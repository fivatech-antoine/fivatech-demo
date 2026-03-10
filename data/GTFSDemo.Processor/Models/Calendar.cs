namespace GTFSDemo.Processor.Models;

public class Calendar
{
    public string ServiceId { get; set; } = "";
    public int Monday { get; set; }
    public int Tuesday { get; set; }
    public int Wednesday { get; set; }
    public int Thursday { get; set; }
    public int Friday { get; set; }
    public int Saturday { get; set; }
    public int Sunday { get; set; }
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";

    public override string? ToString()
    {
        return ServiceId;
    }
}
