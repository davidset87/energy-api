using System.ComponentModel.DataAnnotations;
using EnergyApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace EnergyApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnergyController : ControllerBase
{
    public const int MinDurationHours = 1;
    public const int MaxDurationHours = 6;

    private readonly IEnergyService _energyService;

    public EnergyController(IEnergyService energyService)
    {
        _energyService = energyService;
    }

    // GET /api/energy/mix
    [HttpGet("mix")]
    public async Task<IActionResult> GetEnergyMix()
    {
        try
        {
            var result = await _energyService.GetEnergyMixForThreeDaysAsync();
            return Ok(result);
        }
        catch (ExternalApiException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    // GET /api/energy/optimal-window?durationHours=3
    // Out-of-range or missing values are rejected with a 400 by [ApiController] + [Range].
    [HttpGet("optimal-window")]
    public async Task<IActionResult> GetOptimalWindow(
        [FromQuery, Range(MinDurationHours, MaxDurationHours)] int durationHours)
    {
        try
        {
            var result = await _energyService.GetOptimalChargingWindowAsync(durationHours);
            return Ok(result);
        }
        catch (ExternalApiException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}