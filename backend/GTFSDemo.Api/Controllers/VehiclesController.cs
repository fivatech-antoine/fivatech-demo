using GTFSDemo.Api.Models;
using GTFSDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GTFSDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class VehiclesController(GtfsRealtimeService gtfsRealtime) : ControllerBase
{
    /// <summary>Retourne la position de tous les véhicules en circulation.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VehiclePosition>>(StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(gtfsRealtime.GetVehiclePositions());

    /// <summary>Retourne la position d'un véhicule par son tripId.</summary>
    [HttpGet("{tripId}")]
    [ProducesResponseType<VehiclePosition>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByTripId(string tripId)
    {
        var vehicle = gtfsRealtime.GetVehiclePositions()
            .FirstOrDefault(v => v.TripId == tripId);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }
}
