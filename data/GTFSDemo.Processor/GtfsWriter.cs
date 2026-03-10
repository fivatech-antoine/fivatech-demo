using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using GTFSDemo.Processor.Csv;
using GTFSDemo.Processor.Models;
using Calendar = GTFSDemo.Processor.Models.Calendar;

namespace GTFSDemo.Processor;

public class GtfsWriter(string resultDirectory)
{
    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture)
    {
        // Quoter tous les champs de données, mais pas les en-têtes (fidélité au format source).
        // HeaderRecord est null pendant l'écriture du header, renseigné ensuite.
        ShouldQuote = args => args.Row.HeaderRecord != null,
    };

    public void Write(GtfsFeed feed)
    {
        Directory.CreateDirectory(resultDirectory);

        WriteFile<Agency,       AgencyMap>      ("agency.txt",         feed.Agencies);
        WriteFile<Route,        RouteMap>       ("routes.txt",         feed.Routes);
        WriteFile<Stop,         StopMap>        ("stops.txt",          feed.Stops);
        WriteFile<Trip,         TripMap>        ("trips.txt",          feed.Trips);
        WriteFile<StopTime,     StopTimeMap>    ("stop_times.txt",     feed.StopTimes);
        WriteFile<Calendar,     CalendarMap>    ("calendar.txt",       feed.Calendars);
        WriteFile<CalendarDate, CalendarDateMap>("calendar_dates.txt", feed.CalendarDates);
        WriteFile<FeedInfo,     FeedInfoMap>    ("feed_info.txt",      feed.FeedInfos);
        WriteFile<Frequency,    FrequencyMap>   ("frequencies.txt",    feed.Frequencies);
        WriteFile<Transfer,     TransferMap>    ("transfers.txt",      feed.Transfers);
    }

    private void WriteFile<T, TMap>(string filename, Dictionary<string, T> records)
        where TMap : ClassMap<T>, new()
    {
        var path = Path.Combine(resultDirectory, filename);
        // UTF-8 sans BOM pour une compatibilité maximale avec les outils GTFS
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        using var csv = new CsvWriter(writer, Config);
        csv.Context.RegisterClassMap<TMap>();
        csv.WriteRecords(records.Values);
        Console.WriteLine($"  [OK]   {filename,-30} {records.Count,8} enregistrement(s)");
    }
}
