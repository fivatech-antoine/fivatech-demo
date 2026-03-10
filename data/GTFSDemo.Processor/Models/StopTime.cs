namespace GTFSDemo.Processor.Models;

public class StopTime
{
    public string TripId { get; set; } = "";
    public string ArrivalTime { get; set; } = "";
    public string DepartureTime { get; set; } = "";
    public string StopId { get; set; } = "";
    public int StopSequence { get; set; }
    public string PickupType { get; set; } = "";
    public string DropOffType { get; set; } = "";
    public Trip? Trip { get; set; }
    public Stop? Stop { get; set; }

    public override string? ToString()
    {
        return TripId + " " + StopSequence;
    }
}
