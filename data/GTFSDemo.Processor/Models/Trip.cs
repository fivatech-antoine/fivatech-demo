namespace GTFSDemo.Processor.Models;

public class Trip
{
    public string RouteId { get; set; } = "";
    public string ServiceId { get; set; } = "";
    public string TripId { get; set; } = "";
    public string Headsign { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string DirectionId { get; set; } = "";
    public string BlockId { get; set; } = "";
    public string OriginalTripId { get; set; } = "";
    public string Hints { get; set; } = "";
    public Route? Route { get; set; }
    public List<StopTime> StopTimes { get; set; } = [];
    public override string? ToString()
    {
        return TripId;
    }
    public bool IsInTheBox { get; set; } = false;
}
