using GTFSDemo.Api.Models;
using GTFSDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GTFSDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DeparturesController(
    GtfsStaticService gtfsStatic,
    GtfsRealtimeService gtfsRealtime,
    ILogger<DeparturesController> logger) : ControllerBase
{
    /// <summary>
    /// Retourne les prochains départs pour un arrêt donné,
    /// enrichis avec les données temps réel (retards).
    /// </summary>
    [HttpGet("{stopId}")]
    [ProducesResponseType<IReadOnlyList<Departure>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetDepartures(string stopId, [FromQuery] int limit = 20)
    {
        if (!gtfsStatic.IsLoaded)
            return StatusCode(503, "Données GTFS pas encore chargées.");

        // Vérifie que l'arrêt existe
        if (gtfsStatic.GetStop(stopId) is null)
            return NotFound($"Arrêt introuvable : {stopId}");

        var departures = gtfsStatic.GetDepartures(stopId, limit);

        // Enrichit avec les données temps réel
        var enriched = departures.Select(dep =>
        {
            var delaySeconds = gtfsRealtime.GetTripDelaySeconds(dep.TripId);
            if (delaySeconds is null) return dep;

            dep.DelaySeconds = delaySeconds;
            dep.EstimatedDeparture = dep.ScheduledDeparture.AddSeconds(delaySeconds.Value);
            return dep;
        }).ToList();

        logger.LogDebug("Départs pour {StopId} : {Count}", stopId, enriched.Count);
        return Ok(enriched);
    }
}
