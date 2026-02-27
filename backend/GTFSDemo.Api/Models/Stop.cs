namespace GTFSDemo.Api.Models;

public class Stop
{
    public string StopId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string? PlatformCode { get; set; }
}
