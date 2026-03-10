namespace GTFSDemo.Processor.Models;

public class Stop
{
    public string StopId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Lat { get; set; } = "";
    public string Lon { get; set; } = "";
    public string LocationType { get; set; } = "";
    public string ParentStation { get; set; } = "";
    public string PlatformCode { get; set; } = "";
    public string OriginalStopId { get; set; } = "";
    public bool IsStation => string.IsNullOrEmpty(ParentStation);
    public Stop? Station { get; set; }

    public override string? ToString()
    {
        return StopId;
    }
}
