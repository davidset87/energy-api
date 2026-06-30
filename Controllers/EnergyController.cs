using Microsoft.AspNetCore.Mvc;
using EnergyApi.Services;

namespace EnergyApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnergyController : ControllerBase
{
    private readonly IEnergyService _energyService;

    public EnergyController(IEnergyService energyService)
    {
        _energyService = energyService;
    }

    [HttpGet("mix")]
    public async Task<IActionResult> GetEnergyMix()
    {
        try
        {
            var result = await _energyService.GetEnergyMixForThreeDaysAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("optimal-window")]
    public async Task<IActionResult> GetOptimalWindow([FromQuery] int durationHours)
    {
        if (durationHours < 1 || durationHours > 6)
        {
            return BadRequest(new { error = "durationHours must be between 1 and 6." });
        }

        try
        {
            var result = await _energyService.GetOptimalChargingWindowAsync(durationHours);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}