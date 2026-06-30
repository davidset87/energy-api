namespace EnergyApi.Models;

public class GenerationMix
{
    public string Fuel { get; set; } = string.Empty;
    public double Perc { get; set; }
}

public class IntervalData
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public List<GenerationMix> GenerationMix { get; set; } = new();
}

public class DailyEnergyMix
{
    public string Date { get; set; } = string.Empty;
    public Dictionary<string, double> AverageMix { get; set; } = new();
    public double CleanEnergyPercentage { get; set; }
}

public class OptimalChargingWindow
{
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public double AverageCleanEnergyPercentage { get; set; }
}