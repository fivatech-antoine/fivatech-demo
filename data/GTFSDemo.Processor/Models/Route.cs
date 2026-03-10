using System.Globalization;

namespace GTFSDemo.Processor.Models;

public class Route
{
    public string RouteId { get; set; } = "";
    public string AgencyId { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string LongName { get; set; } = "";
    public string Desc { get; set; } = "";
    public string RouteType { get; set; } = "";
    public Agency? Agency { get; set; }
    public List<Trip> Trips { get; set; } = [];
    public override string? ToString()
    {
        return RouteId;
    }

    public bool IsInTheBox { get; set; } = false;

    public void CalculateBox(double minLat, double maxLat, double minLon, double maxLon)
    {
        foreach (Trip trip in Trips)
        {
            foreach (StopTime stopTime in trip.StopTimes)
            {
                if (stopTime.StopSequence != trip.StopTimes.Count) // si seule la destination finale est dans la box, ça ne suffit pas
                {
                    if (double.TryParse(stopTime.Stop?.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude)
                        && double.TryParse(stopTime.Stop?.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
                    {
                        if (minLat <= latitude && latitude <= maxLat && minLon <= longitude && longitude <= maxLon)
                        {
                            trip.IsInTheBox = true;
                            IsInTheBox = true;
                            break;
                        }
                    }
                }
            }
        }
        Trips = Trips.Where(t => t.IsInTheBox).ToList();
    }
}
