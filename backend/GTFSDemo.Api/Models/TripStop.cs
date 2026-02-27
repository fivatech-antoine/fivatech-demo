namespace GTFSDemo.Api.Models;

public class TripStop
{
    public string StopId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int Sequence { get; set; }
    public DateTime ScheduledDeparture { get; set; }
}
