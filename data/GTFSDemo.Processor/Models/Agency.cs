namespace GTFSDemo.Processor.Models;

public class Agency
{
    public string AgencyId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Timezone { get; set; } = "";
    public string Lang { get; set; } = "";
    public string Phone { get; set; } = "";
    public List<Route> Routes { get; set; } = [];
    public override string? ToString()
    {
        return AgencyId;
    }

    public bool IsInTheBox { get; set; } = false;

}
