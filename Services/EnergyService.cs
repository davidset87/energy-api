using EnergyApi.Models;
using System.Text.Json;

namespace EnergyApi.Services;

public class EnergyService : IEnergyService
{
    private readonly HttpClient _httpClient;
    private static readonly string[] CleanSources = { "biomass", "nuclear", "hydro", "wind", "solar" };

    public EnergyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.carbonintensity.org.uk/");
    }

    private async Task<List<IntervalData>> FetchIntervalsAsync(DateTime from, DateTime to)
    {
        string fromStr = from.ToString("yyyy-MM-ddTHH:mmZ");
        string toStr = to.ToString("yyyy-MM-ddTHH:mmZ");

        var url = $"generation/{fromStr}/{toStr}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var intervals = new List<IntervalData>();
        var dataArray = doc.RootElement.GetProperty("data");

        foreach (var item in dataArray.EnumerateArray())
        {
            var interval = new IntervalData
            {
                From = item.GetProperty("from").GetString() ?? "",
                To = item.GetProperty("to").GetString() ?? "",
                GenerationMix = new List<GenerationMix>()
            };

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

    private double CalculateCleanPercentage(List<GenerationMix> mix)
    {
        return mix.Where(m => CleanSources.Contains(m.Fuel)).Sum(m => m.Perc);
    }

    public async Task<List<DailyEnergyMix>> GetEnergyMixForThreeDaysAsync()
    {
        var now = DateTime.UtcNow;
        var start = now.Date; 
        var end = start.AddDays(3); 

        var intervals = await FetchIntervalsAsync(start, end);

        
        var groupedByDay = intervals.GroupBy(i => DateTime.Parse(i.From).Date);

        var result = new List<DailyEnergyMix>();

        foreach (var dayGroup in groupedByDay.OrderBy(g => g.Key))
        {
            var allFuels = dayGroup.SelectMany(i => i.GenerationMix).GroupBy(m => m.Fuel);

            var averageMix = allFuels.ToDictionary(
                f => f.Key,
                f => Math.Round(f.Average(m => m.Perc), 2)
            );

            double cleanPercentage = averageMix
                .Where(kv => CleanSources.Contains(kv.Key))
                .Sum(kv => kv.Value);

            result.Add(new DailyEnergyMix
            {
                Date = dayGroup.Key.ToString("yyyy-MM-dd"),
                AverageMix = averageMix,
                CleanEnergyPercentage = Math.Round(cleanPercentage, 2)
            });
        }

        return result;
    }

    public async Task<OptimalChargingWindow> GetOptimalChargingWindowAsync(int durationHours)
    {
        if (durationHours < 1 || durationHours > 6)
            throw new ArgumentException("Duration must be between 1 and 6 hours.");

        var now = DateTime.UtcNow;
        var start = now.Date;
        var end = start.AddDays(2); // dwa kolejne dni

        var intervals = await FetchIntervalsAsync(start, end);
        intervals = intervals.OrderBy(i => DateTime.Parse(i.From)).ToList();

        int slotsNeeded = durationHours * 2; // 30-min slots

        if (intervals.Count < slotsNeeded)
            throw new InvalidOperationException("Not enough data to calculate window.");

        double bestAverage = -1;
        int bestStartIndex = 0;

        for (int i = 0; i <= intervals.Count - slotsNeeded; i++)
        {
            var window = intervals.Skip(i).Take(slotsNeeded);
            double avgClean = window.Average(interval => CalculateCleanPercentage(interval.GenerationMix));

            if (avgClean > bestAverage)
            {
                bestAverage = avgClean;
                bestStartIndex = i;
            }
        }

        var windowStart = DateTime.Parse(intervals[bestStartIndex].From);
        var windowEnd = DateTime.Parse(intervals[bestStartIndex + slotsNeeded - 1].To);

        return new OptimalChargingWindow
        {
            StartDateTime = windowStart,
            EndDateTime = windowEnd,
            AverageCleanEnergyPercentage = Math.Round(bestAverage, 2)
        };
    }
}