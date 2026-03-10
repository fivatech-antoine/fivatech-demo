namespace GTFSDemo.Processor.Models;

public class Transfer
{
    public string FromStopId { get; set; } = "";
    public string ToStopId { get; set; } = "";
    public string FromRouteId { get; set; } = "";
    public string ToRouteId { get; set; } = "";
    public string FromTripId { get; set; } = "";
    public string ToTripId { get; set; } = "";
    public int TransferType { get; set; }
    public string MinTransferTime { get; set; } = "";
    public override string? ToString()
    {
        return FromStopId + FromRouteId + FromTripId + ToStopId + ToRouteId + ToTripId;
    }
}
