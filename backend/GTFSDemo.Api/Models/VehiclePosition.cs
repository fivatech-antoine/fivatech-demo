namespace GTFSDemo.Api.Models;

public class VehiclePosition
{
    public string VehicleId { get; set; } = string.Empty;
    public string TripId { get; set; } = string.Empty;
    public string RouteShortName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Cap en degrés (0 = nord, 90 = est)</summary>
    public float? Bearing { get; set; }

    /// <summary>Vitesse en m/s</summary>
    public float? Speed { get; set; }

    public DateTime Timestamp { get; set; }
    public string CurrentStatus { get; set; } = string.Empty;
}
