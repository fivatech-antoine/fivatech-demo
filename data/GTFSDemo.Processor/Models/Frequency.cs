namespace GTFSDemo.Processor.Models;

public class Frequency
{
    public string TripId { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public int HeadwaySecs { get; set; }
    public string ExactTimes { get; set; } = "";

    public override string? ToString()
    {
        return TripId;
    }
}
