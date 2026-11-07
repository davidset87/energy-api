using System.Net;
using EnergyApi.Services;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Contrib.HttpClient;

namespace EnergyApi.Tests;

public class EnergyServiceTests
{
    private const string BaseUrl = "https://api.carbonintensity.org.uk/";

    // Fixed clock: "now" is always 2026-01-01 10:00 UTC.
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static string BuildIntervalJson(string from, string to, params (string fuel, double perc)[] mix)
    {
        var mixJson = string.Join(",", mix.Select(m => $"{{\"fuel\":\"{m.fuel}\",\"perc\":{m.perc}}}"));
        return $"{{\"from\":\"{from}\",\"to\":\"{to}\",\"generationmix\":[{mixJson}]}}";
    }

    private static string BuildResponse(params string[] intervals) =>
        $"{{\"data\":[{string.Join(",", intervals)}]}}";

    private static EnergyService CreateService(Mock<HttpMessageHandler> handler)
    {
        var httpClient = handler.CreateClient();
        httpClient.BaseAddress = new Uri(BaseUrl);
        return new EnergyService(httpClient, new FakeTimeProvider(FixedNow));
    }

    private static (EnergyService service, Mock<HttpMessageHandler> handler) CreateServiceWithResponse(
        string body, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ReturnsResponse(statusCode, body, "application/json");
        return (CreateService(handler), handler);
    }

    // ---------- Energy mix ----------

    [Fact]
    public async Task GetEnergyMix_AveragesIntervalsOfTheSameDay()
    {
        // Arrange
        var json = BuildResponse(
            BuildIntervalJson("2026-01-01T00:00Z", "2026-01-01T00:30Z", ("wind", 20), ("gas", 80)),
            BuildIntervalJson("2026-01-01T00:30Z", "2026-01-01T01:00Z", ("wind", 40), ("gas", 60)));
        var (service, _) = CreateServiceWithResponse(json);

        // Act
        var result = await service.GetEnergyMixForThreeDaysAsync();

        // Assert
        var day = Assert.Single(result);
        Assert.Equal("2026-01-01", day.Date);
        Assert.Equal(30, day.AverageMix["wind"]);
        Assert.Equal(70, day.AverageMix["gas"]);
        Assert.Equal(30, day.CleanEnergyPercentage);
    }

    [Fact]
    public async Task GetEnergyMix_SplitsIntervalsByUtcDay_AndSortsThem()
    {
        // Arrange - intervals deliberately out of order, spanning midnight UTC
        var json = BuildResponse(
            BuildIntervalJson("2026-01-02T00:00Z", "2026-01-02T00:30Z", ("wind", 80), ("gas", 20)),
            BuildIntervalJson("2026-01-01T23:30Z", "2026-01-02T00:00Z", ("wind", 10), ("gas", 90)));
        var (service, _) = CreateServiceWithResponse(json);

        // Act
        var result = await service.GetEnergyMixForThreeDaysAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("2026-01-01", result[0].Date);
        Assert.Equal(10, result[0].CleanEnergyPercentage);
        Assert.Equal("2026-01-02", result[1].Date);
        Assert.Equal(80, result[1].CleanEnergyPercentage);
    }

    [Fact]
    public async Task GetEnergyMix_RequestsThreeDaysStartingTodayUtc()
    {
        // Arrange
        var (service, handler) = CreateServiceWithResponse(BuildResponse());

        // Act
        await service.GetEnergyMixForThreeDaysAsync();

        // Assert
        handler.VerifyRequest(
            HttpMethod.Get,
            $"{BaseUrl}generation/2026-01-01T00:00Z/2026-01-04T00:00Z",
            Times.Once());
    }

    // ---------- Optimal window ----------

    [Fact]
    public async Task GetOptimalWindow_PicksWindowWithHighestAverageCleanEnergy()
    {
        // Arrange - 2 hours of data, we ask for 1 hour (2 slots)
        var json = BuildResponse(
            BuildIntervalJson("2026-01-01T00:00Z", "2026-01-01T00:30Z", ("wind", 10)),
            BuildIntervalJson("2026-01-01T00:30Z", "2026-01-01T01:00Z", ("wind", 10)),
            BuildIntervalJson("2026-01-01T01:00Z", "2026-01-01T01:30Z", ("wind", 90)),
            BuildIntervalJson("2026-01-01T01:30Z", "2026-01-01T02:00Z", ("wind", 90)));
        var (service, _) = CreateServiceWithResponse(json);

        // Act
        var result = await service.GetOptimalChargingWindowAsync(1);

        // Assert
        Assert.Equal(90, result.AverageCleanEnergyPercentage);
        Assert.Equal(new DateTime(2026, 1, 1, 1, 0, 0), result.StartDateTime);
        Assert.Equal(new DateTime(2026, 1, 1, 2, 0, 0), result.EndDateTime);
    }

    [Fact]
    public async Task GetOptimalWindow_CanSpanAcrossMidnight()
    {
        // Arrange - the best window starts on one day and ends on the next
        var json = BuildResponse(
            BuildIntervalJson("2026-01-01T23:00Z", "2026-01-01T23:30Z", ("wind", 5)),
            BuildIntervalJson("2026-01-01T23:30Z", "2026-01-02T00:00Z", ("wind", 95)),
            BuildIntervalJson("2026-01-02T00:00Z", "2026-01-02T00:30Z", ("wind", 95)),
            BuildIntervalJson("2026-01-02T00:30Z", "2026-01-02T01:00Z", ("wind", 5)));
        var (service, _) = CreateServiceWithResponse(json);

        // Act
        var result = await service.GetOptimalChargingWindowAsync(1);

        // Assert
        Assert.Equal(95, result.AverageCleanEnergyPercentage);
        Assert.Equal(new DateTime(2026, 1, 1, 23, 30, 0), result.StartDateTime);
        Assert.Equal(new DateTime(2026, 1, 2, 0, 30, 0), result.EndDateTime);
    }

    [Fact]
    public async Task GetOptimalWindow_RequestsTwoDaysStartingTodayUtc()
    {
        // Arrange
        var json = BuildResponse(
            BuildIntervalJson("2026-01-01T00:00Z", "2026-01-01T00:30Z", ("wind", 50)),
            BuildIntervalJson("2026-01-01T00:30Z", "2026-01-01T01:00Z", ("wind", 50)));
        var (service, handler) = CreateServiceWithResponse(json);

        // Act
        await service.GetOptimalChargingWindowAsync(1);

        // Assert
        handler.VerifyRequest(
            HttpMethod.Get,
            $"{BaseUrl}generation/2026-01-01T00:00Z/2026-01-03T00:00Z",
            Times.Once());
    }

    [Fact]
    public async Task GetOptimalWindow_ThrowsExternalApiException_WhenNotEnoughData()
    {
        // Arrange - 1 slot of data but 1 hour (2 slots) requested
        var json = BuildResponse(
            BuildIntervalJson("2026-01-01T00:00Z", "2026-01-01T00:30Z", ("wind", 50)));
        var (service, _) = CreateServiceWithResponse(json);

        // Act & Assert
        await Assert.ThrowsAsync<ExternalApiException>(() => service.GetOptimalChargingWindowAsync(1));
    }

    // ---------- Upstream failures ----------

    [Fact]
    public async Task GetEnergyMix_ThrowsExternalApiException_WhenUpstreamReturnsError()
    {
        var (service, _) = CreateServiceWithResponse("", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<ExternalApiException>(() => service.GetEnergyMixForThreeDaysAsync());
    }

    [Fact]
    public async Task GetEnergyMix_ThrowsExternalApiException_WhenBodyIsEmpty()
    {
        var (service, _) = CreateServiceWithResponse("");

        await Assert.ThrowsAsync<ExternalApiException>(() => service.GetEnergyMixForThreeDaysAsync());
    }

    [Fact]
    public async Task GetEnergyMix_ThrowsExternalApiException_WhenJsonIsMalformed()
    {
        var (service, _) = CreateServiceWithResponse("{not valid json");

        await Assert.ThrowsAsync<ExternalApiException>(() => service.GetEnergyMixForThreeDaysAsync());
    }

    [Fact]
    public async Task GetEnergyMix_ThrowsExternalApiException_WhenDataFieldIsMissing()
    {
        var (service, _) = CreateServiceWithResponse("{\"other\":[]}");

        await Assert.ThrowsAsync<ExternalApiException>(() => service.GetEnergyMixForThreeDaysAsync());
    }

    [Fact]
    public async Task GetEnergyMix_ThrowsExternalApiException_WhenIntervalFieldsAreMissing()
    {
        var (service, _) = CreateServiceWithResponse("{\"data\":[{\"from\":\"2026-01-01T00:00Z\"}]}");

        await Assert.ThrowsAsync<ExternalApiException>(() => service.GetEnergyMixForThreeDaysAsync());
    }

    [Fact]
    public async Task GetEnergyMix_ThrowsExternalApiException_WhenUpstreamTimesOut()
    {
        // Arrange - HttpClient reports a timeout as TaskCanceledException
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ThrowsAsync(new TaskCanceledException());
        var service = CreateService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<ExternalApiException>(() => service.GetEnergyMixForThreeDaysAsync());
    }

    [Fact]
    public async Task GetEnergyMix_ThrowsExternalApiException_WhenUpstreamIsUnreachable()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ThrowsAsync(new HttpRequestException("connection refused"));
        var service = CreateService(handler);

        // Act & Assert
        await Assert.ThrowsAsync<ExternalApiException>(() => service.GetEnergyMixForThreeDaysAsync());
    }
}