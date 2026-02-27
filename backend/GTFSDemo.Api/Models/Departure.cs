namespace GTFSDemo.Api.Models;

public class Departure
{
    public string TripId { get; set; } = string.Empty;
    public string RouteShortName { get; set; } = string.Empty;
    public string Headsign { get; set; } = string.Empty;

    /// <summary>Heure théorique (ISO 8601)</summary>
    public DateTime ScheduledDeparture { get; set; }

    /// <summary>Heure estimée temps réel (null si pas de données RT)</summary>
    public DateTime? EstimatedDeparture { get; set; }

    /// <summary>Retard en secondes (positif = en retard, négatif = en avance)</summary>
    public int? DelaySeconds { get; set; }

    public string StopId { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Operator { get; set; }
}
