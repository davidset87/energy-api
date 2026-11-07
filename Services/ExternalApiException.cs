namespace EnergyApi.Services;

public class ExternalApiException : Exception
{
    public ExternalApiException(string message) : base(message) { }
}