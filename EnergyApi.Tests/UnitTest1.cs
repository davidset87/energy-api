using EnergyApi.Models;
using EnergyApi.Services;
using Moq;

namespace EnergyApi.Tests;

public class EnergyServiceTests
{
    [Fact]
    public async Task GetEnergyMix_ReturnThreeDays()
    {
        // Arrange
        var mockService = new Mock<IEnergyService>();
        mockService.Setup(s => s.GetEnergyMixForThreeDaysAsync())
            .ReturnsAsync(new List<DailyEnergyMix>
            {
                new DailyEnergyMix { Date = "2026-06-30", CleanEnergyPercentage = 38.5 },
                new DailyEnergyMix { Date = "2026-07-01", CleanEnergyPercentage = 47.2 },
                new DailyEnergyMix { Date = "2026-07-02", CleanEnergyPercentage = 78.4 }
            });

        // Act
        var result = await mockService.Object.GetEnergyMixForThreeDaysAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("2026-06-30", result[0].Date);
        Assert.Equal(78.4, result[2].CleanEnergyPercentage);
    }

    [Fact]
    public async Task GetOptimalWindow_ReturnsBestWindow()
    {
        // Arrange
        var mockService = new Mock<IEnergyService>();
        mockService.Setup(s => s.GetOptimalChargingWindowAsync(3))
            .ReturnsAsync(new OptimalChargingWindow
            {
                StartDateTime = new DateTime(2026, 7, 1, 23, 0, 0),
                EndDateTime = new DateTime(2026, 7, 2, 2, 0, 0),
                AverageCleanEnergyPercentage = 70.28
            });

        // Act
        var result = await mockService.Object.GetOptimalChargingWindowAsync(3);

        // Assert
        Assert.Equal(70.28, result.AverageCleanEnergyPercentage);
        Assert.Equal(new DateTime(2026, 7, 1, 23, 0, 0), result.StartDateTime);
    }

    [Fact]
    public async Task GetOptimalWindow_InvalidDuration_ThrowsException()
    {
        // Arrange
        var mockService = new Mock<IEnergyService>();
        mockService.Setup(s => s.GetOptimalChargingWindowAsync(It.Is<int>(d => d < 1 || d > 6)))
            .ThrowsAsync(new ArgumentException("Duration must be between 1 and 6 hours."));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            mockService.Object.GetOptimalChargingWindowAsync(0));
    }
}