using GTFSDemo.Api.Models;
using GTFSDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GTFSDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StopsController(GtfsStaticService gtfsStatic, ILogger<StopsController> logger)
    : ControllerBase
{
    /// <summary>Recherche des arrêts par nom (min 2 caractères).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Stop>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Search([FromQuery] string query = "")
    {
        if (!gtfsStatic.IsLoaded)
            return StatusCode(503, "Données GTFS pas encore chargées, réessayez dans quelques secondes.");

        if (query.Length < 2)
            return Ok(Array.Empty<Stop>());

        var results = gtfsStatic.SearchStops(query);
        logger.LogDebug("Recherche '{Query}' → {Count} résultats", query, results.Count);
        return Ok(results);
    }

    /// <summary>Retourne un arrêt par son identifiant GTFS.</summary>
    [HttpGet("{stopId}")]
    [ProducesResponseType<Stop>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(string stopId)
    {
        var stop = gtfsStatic.GetStop(stopId);
        return stop is null ? NotFound() : Ok(stop);
    }
}
