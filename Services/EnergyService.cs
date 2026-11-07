using System.Globalization;
using System.Text.Json;
using EnergyApi.Models;

namespace EnergyApi.Services;

public class EnergyService : IEnergyService
{
    // The Carbon Intensity API returns one data point every 30 minutes.
    private const int SlotsPerHour = 2;
    private const int MixForecastDays = 3;
    private const int OptimalWindowSearchDays = 2;
    private const string ApiDateFormat = "yyyy-MM-ddTHH:mmZ";

    private static readonly HashSet<string> CleanSources =
        new(StringComparer.OrdinalIgnoreCase) { "biomass", "nuclear", "hydro", "wind", "solar" };

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    public EnergyService(HttpClient httpClient, TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
    }

    private static DateTime ParseUtc(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).UtcDateTime;

    private async Task<List<IntervalData>> FetchIntervalsAsync(DateTime from, DateTime to)
    {
        var fromStr = from.ToString(ApiDateFormat, CultureInfo.InvariantCulture);
        var toStr = to.ToString(ApiDateFormat, CultureInfo.InvariantCulture);
        var url = $"generation/{fromStr}/{toStr}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url);
        }
        catch (TaskCanceledException)
        {
            throw new ExternalApiException("The Carbon Intensity API did not respond in time.");
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalApiException($"Could not reach the Carbon Intensity API: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalApiException(
                $"Carbon Intensity API returned {(int)response.StatusCode} {response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ExternalApiException("Carbon Intensity API returned an empty response.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new ExternalApiException("Carbon Intensity API returned malformed JSON.");
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("data", out var dataArray))
            {
                throw new ExternalApiException("Carbon Intensity API response is missing the 'data' field.");
            }

            try
            {
                var intervals = new List<IntervalData>();

                foreach (var item in dataArray.EnumerateArray())
                {
                    var interval = new IntervalData
                    {
                        From = item.GetProperty("from").GetString() ?? "",
                        To = item.GetProperty("to").GetString() ?? "",
                        GenerationMix = new List<GenerationMix>()
                    };

                    // Fail early if a date is not valid, instead of crashing later.
                    _ = ParseUtc(interval.From);
                    _ = ParseUtc(interval.To);

                    foreach (var mix in item.GetProperty("generationmix").EnumerateArray())
                    {
                        interval.GenerationMix.Add(new GenerationMix
                        {
                            Fuel = mix.GetProperty("fuel").GetString() ?? "",
                            Perc = mix.GetProperty("perc").GetDouble()
                        });
                    }

                    intervals.Add(interval);
                }

                return intervals;
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
            {
                throw new ExternalApiException("Carbon Intensity API response has an unexpected structure.");
            }
        }
    }

    private static double CalculateCleanPercentage(IEnumerable<GenerationMix> mix) =>
        mix.Where(m => CleanSources.Contains(m.Fuel)).Sum(m => m.Perc);

    public async Task<List<DailyEnergyMix>> GetEnergyMixForThreeDaysAsync()
    {
        var start = _timeProvider.GetUtcNow().UtcDateTime.Date;
        var end = start.AddDays(MixForecastDays);

        var intervals = await FetchIntervalsAsync(start, end);

        var groupedByDay = intervals.GroupBy(i => ParseUtc(i.From).Date);

        var result = new List<DailyEnergyMix>();

        foreach (var dayGroup in groupedByDay.OrderBy(g => g.Key))
        {
            var averageMix = dayGroup
                .SelectMany(i => i.GenerationMix)
                .GroupBy(m => m.Fuel)
                .ToDictionary(f => f.Key, f => Math.Round(f.Average(m => m.Perc), 2));

            var cleanPercentage = averageMix
                .Where(kv => CleanSources.Contains(kv.Key))
                .Sum(kv => kv.Value);

            result.Add(new DailyEnergyMix
            {
                Date = dayGroup.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                AverageMix = averageMix,
                CleanEnergyPercentage = Math.Round(cleanPercentage, 2)
            });
        }

        return result;
    }

    // durationHours is validated by [Range] on the controller (single source of truth).
    public async Task<OptimalChargingWindow> GetOptimalChargingWindowAsync(int durationHours)
    {
        var start = _timeProvider.GetUtcNow().UtcDateTime.Date;
        var end = start.AddDays(OptimalWindowSearchDays);

        var intervals = await FetchIntervalsAsync(start, end);
        intervals = intervals.OrderBy(i => ParseUtc(i.From)).ToList();

        var slotsNeeded = durationHours * SlotsPerHour;

        if (intervals.Count < slotsNeeded)
        {
            throw new ExternalApiException("The Carbon Intensity API returned too little data to calculate a window.");
        }

        var bestAverage = -1.0;
        var bestStartIndex = 0;

        for (var i = 0; i <= intervals.Count - slotsNeeded; i++)
        {
            var avgClean = intervals
                .Skip(i)
                .Take(slotsNeeded)
                .Average(interval => CalculateCleanPercentage(interval.GenerationMix));

            if (avgClean > bestAverage)
            {
                bestAverage = avgClean;
                bestStartIndex = i;
            }
        }

        return new OptimalChargingWindow
        {
            StartDateTime = ParseUtc(intervals[bestStartIndex].From),
            EndDateTime = ParseUtc(intervals[bestStartIndex + slotsNeeded - 1].To),
            AverageCleanEnergyPercentage = Math.Round(bestAverage, 2)
        };
    }
}