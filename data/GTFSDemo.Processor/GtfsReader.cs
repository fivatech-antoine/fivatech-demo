using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using GTFSDemo.Processor.Csv;
using GTFSDemo.Processor.Models;
using Calendar = GTFSDemo.Processor.Models.Calendar;

namespace GTFSDemo.Processor;

public class GtfsReader(string sourceDirectory)
{
    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
        MissingFieldFound = null,
        HeaderValidated = null,
        BadDataFound = null,
    };

    public GtfsFeed Read()
    {
        return new GtfsFeed
        {
            Agencies    = ReadFile<Agency,       AgencyMap>      ("agency.txt"),
            Routes      = ReadFile<Route,        RouteMap>       ("routes.txt"),
            Stops       = ReadFile<Stop,         StopMap>        ("stops.txt"),
            Trips       = ReadFile<Trip,         TripMap>        ("trips.txt"),
            StopTimes   = ReadFile<StopTime,     StopTimeMap>    ("stop_times.txt"),
            Calendars   = ReadFile<Calendar,     CalendarMap>    ("calendar.txt"),
            CalendarDates = ReadFile<CalendarDate, CalendarDateMap>("calendar_dates.txt"),
            FeedInfos   = ReadFile<FeedInfo,     FeedInfoMap>    ("feed_info.txt"),
            Frequencies = ReadFile<Frequency,    FrequencyMap>   ("frequencies.txt"),
            Transfers   = ReadFile<Transfer,     TransferMap>    ("transfers.txt"),
        };
    }

    private Dictionary<string, T> ReadFile<T, TMap>(string filename)
        where TMap : ClassMap<T>, new()
    {
        var path = Path.Combine(sourceDirectory, filename);
        if (!File.Exists(path))
        {
            Console.WriteLine($"  [SKIP] {filename} — fichier absent");
            return [];
        }

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, Config);
        csv.Context.RegisterClassMap<TMap>();
        var records = csv.GetRecords<T>().ToDictionary(e => e?.ToString()??"");
        Console.WriteLine($"  [OK]   {filename,-30} {records.Count,8} enregistrement(s)");
        return records;
    }
}
