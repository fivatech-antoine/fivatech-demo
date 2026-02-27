using GTFSDemo.Api.Models;
using GTFSDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GTFSDemo.Api.Controllers;

[ApiController]
[Route("api/trips")]
public class TripsController(GtfsStaticService gtfsStatic) : ControllerBase
{
    /// <summary>
    /// Retourne les arrêts restants d'un trajet depuis un arrêt donné jusqu'au terminus.
    /// </summary>
    /// <param name="tripId">Identifiant du trajet GTFS.</param>
    /// <param name="fromStopId">Arrêt de départ (inclus). Si absent, retourne tout le trajet.</param>
    [HttpGet("{tripId}/stops")]
    public ActionResult<IReadOnlyList<TripStop>> GetTripStops(
        string tripId,
        [FromQuery] string? fromStopId)
    {
        if (!gtfsStatic.IsLoaded)
            return StatusCode(503, "Données GTFS pas encore chargées");

        var stops = gtfsStatic.GetTripRemainingStops(tripId, fromStopId ?? "");
        if (stops.Count == 0)
            return NotFound($"Trajet {tripId} introuvable ou sans arrêts.");

        return Ok(stops);
    }
}
