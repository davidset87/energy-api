using EnergyApi.Models;

namespace EnergyApi.Services;

public interface IEnergyService
{
    Task<List<DailyEnergyMix>> GetEnergyMixForThreeDaysAsync();
    Task<OptimalChargingWindow> GetOptimalChargingWindowAsync(int durationHours);
}