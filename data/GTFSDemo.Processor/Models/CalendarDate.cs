namespace GTFSDemo.Processor.Models;

public class CalendarDate
{
    public string ServiceId { get; set; } = "";
    public string Date { get; set; } = "";
    public int ExceptionType { get; set; }
    public override string? ToString()
    {
        return ServiceId + " " + Date;
    }
}
