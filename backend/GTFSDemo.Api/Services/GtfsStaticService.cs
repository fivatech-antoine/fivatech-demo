using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using GTFSDemo.Api.Configuration;
using GTFSDemo.Api.Models;
using Microsoft.Extensions.Options;

namespace GTFSDemo.Api.Services;

/// <summary>
/// Charge le GTFS statique au démarrage, filtre les données sur les services actifs
/// du jour courant, puis recharge chaque jour à 03h00.
/// Le swap de données est atomique : les requêtes en cours continuent avec l'ancienne
/// version jusqu'à ce que le nouveau chargement soit complet.
/// </summary>
public class GtfsStaticService(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenTransportDataOptions> options,
    ILogger<GtfsStaticService> logger) : BackgroundService
{
    private readonly OpenTransportDataOptions _options = options.Value;

    // Snapshot courant — remplacé atomiquement lors de chaque rechargement
    private volatile DataHolder? _data;

    public bool IsLoaded => _data != null;

    // ── BackgroundService ────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryLoadAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextReload();
            logger.LogInformation(
                "Prochain rechargement GTFS dans {Hours}h{Minutes:D2}m (à 03:00)",
                (int)delay.TotalHours, delay.Minutes);

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }

            await TryLoadAsync(stoppingToken);
        }
    }

    private async Task TryLoadAsync(CancellationToken ct)
    {
        logger.LogInformation("Chargement du GTFS statique depuis {Url}", _options.GtfsStaticUrl);
        try
        {
            var data = await LoadAsync(ct);
            _data = data;           // swap atomique
            logger.LogInformation(
                "GTFS chargé pour le {Date} : {Stations} stations, {Stops} quais, " +
                "{Routes} lignes, {Trips} trajets actifs, {StopTimes} horaires",
                data.Date, data.Stations.Count, data.AllStops.Count,
                data.Routes.Count, data.Trips.Count, data.StopTimes.Count);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Échec du chargement GTFS — données précédentes conservées");
        }
    }

    /// <summary>Délai jusqu'au prochain 03:00 (aujourd'hui si avant 03:00, sinon demain).</summary>
    private static TimeSpan TimeUntilNextReload()
    {
        var now  = DateTime.Now;
        var next = DateTime.Today.AddHours(3);
        if (now >= next) next = next.AddDays(1);
        return next - now;
    }

    // ── API publique ─────────────────────────────────────────────────────────

    public IReadOnlyList<Station> SearchStops(string query, int limit = 50)
    {
        var data = _data;
        if (data == null || string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        return data.Stations.Values
            .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name)
            .Take(limit)
            .Select(ToModel)
            .ToList();
    }

    public Station? GetStop(string stopId)
    {
        var data = _data;
        return data != null && data.Stations.TryGetValue(stopId, out var s) ? ToModel(s) : null;
    }

    public IReadOnlyList<Departure> GetDepartures(string stopId, int limit = 20)
    {
        var data = _data;
        if (data == null) return [];

        if (!data.StopsByStation.TryGetValue(stopId, out var platformIds))
            return [];

        // Collecter les stop_times de l'arrêt et de ses quais
        var stopTimes = new List<StopTimeData>();
        if (data.StopTimesByStopId.TryGetValue(stopId, out var directIds))
            foreach (int id in directIds) stopTimes.Add(data.StopTimes[id]);

        foreach (string platformId in platformIds)
            if (data.StopTimesByStopId.TryGetValue(platformId, out var ptIds))
                foreach (int id in ptIds) stopTimes.Add(data.StopTimes[id]);

        if (stopTimes.Count == 0) return [];

        // Les trajets sont déjà filtrés sur les services actifs du jour de chargement.
        // Comparaison sur DateTime réel pour gérer correctement les départs post-minuit
        // (GTFS stocke 24:30 pour 00:30 le lendemain) : NormalizeTime seul ferait
        // disparaître ces départs avant minuit car 00:30 < 23:50.
        var cutoff = DateTime.Now.AddMinutes(-1);

        return stopTimes
            .Where(st =>
            {
                if (!data.Trips.TryGetValue(st.TripId, out _)) return false;

                // Exclure si c'est le terminus de ce trajet
                if (data.StopTimesByTripId.TryGetValue(st.TripId, out var tripStopIds))
                {
                    var tripStops = tripStopIds.Select(id => data.StopTimes[id]).ToList();
                    int maxSeq = tripStops.Max(s => s.StopSequence);
                    var last   = tripStops.FirstOrDefault(s => s.StopSequence == maxSeq);
                    if (last != null && platformIds.Contains(last.StopId))
                        return false;
                }

                return BuildDepartureDateTime(data.Date, st.DepartureTime) >= cutoff;
            })
            .OrderBy(st => BuildDepartureDateTime(data.Date, st.DepartureTime))
            .Take(limit)
            .Select(st =>
            {
                data.Trips.TryGetValue(st.TripId, out var trip);
                data.Routes.TryGetValue(trip?.RouteId ?? "", out var route);
                data.Agencies.TryGetValue(route?.AgencyId ?? "", out var agency);
                return new Departure
                {
                    TripId             = st.TripId,
                    RouteShortName     = route?.ShortName ?? "",
                    Headsign           = trip?.Headsign ?? "",
                    ScheduledDeparture = BuildDepartureDateTime(data.Date, st.DepartureTime),
                    StopId             = stopId,
                    Operator           = agency?.Name,
                };
            })
            .ToList();
    }

    public string GetRouteShortNameForTrip(string tripId)
    {
        var data = _data;
        if (data == null) return "";
        if (!data.Trips.TryGetValue(tripId, out var trip)) return "";
        if (!data.Routes.TryGetValue(trip.RouteId, out var route)) return "";
        return route.ShortName;
    }

    /// <summary>
    /// Retourne les arrêts restants d'un trajet à partir d'un arrêt donné (inclus).
    /// Si <paramref name="fromStopId"/> est vide, retourne tous les arrêts du trajet.
    /// </summary>
    public IReadOnlyList<TripStop> GetTripRemainingStops(string tripId, string fromStopId)
    {
        var data = _data;
        if (data == null) return [];

        if (!data.StopTimesByTripId.TryGetValue(tripId, out var stopTimeIds))
            return [];

        var sorted = stopTimeIds
            .Select(id => data.StopTimes[id])
            .OrderBy(st => st.StopSequence)
            .ToList();

        int fromSeq = 0;
        if (!string.IsNullOrEmpty(fromStopId))
        {
            var match = sorted.FirstOrDefault(st => st.StopId == fromStopId);
            if (match == null)
            {
                var childIds = data.StopsByStation.TryGetValue(fromStopId, out var children)
                    ? children.ToHashSet()
                    : [];
                match = sorted.FirstOrDefault(st => childIds.Contains(st.StopId));
            }
            if (match != null) fromSeq = match.StopSequence;
        }

        return sorted
            .Where(st => st.StopSequence >= fromSeq)
            .Select(st =>
            {
                StationData? station = null;
                if (data.AllStops.TryGetValue(st.StopId, out var stop))
                    data.Stations.TryGetValue(stop.ParentStation, out station);
                return new TripStop
                {
                    StopId             = st.StopId,
                    Name               = station?.Name ?? st.StopId,
                    Lat                = stop?.Lat ?? 0,
                    Lon                = stop?.Lon ?? 0,
                    Sequence           = st.StopSequence,
                    ScheduledDeparture = BuildDepartureDateTime(data.Date, st.DepartureTime),
                };
            })
            .ToList();
    }

    // ── Chargement GTFS ──────────────────────────────────────────────────────

    private async Task<DataHolder> LoadAsync(CancellationToken ct)
    {
        var zipBytes = await DownloadZipAsync(ct);
        using var zipStream = new MemoryStream(zipBytes);
        using var archive   = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var today = DateOnly.FromDateTime(DateTime.Now);

        // Stops, lignes et agences : chargés intégralement (taille raisonnable)
        var (stations, stopsByStation, allStops) = ParseStops(archive);
        var routes   = ParseRoutes(archive);
        var agencies = ParseAgencies(archive);

        // Calendriers : uniquement pour déterminer les services actifs aujourd'hui
        var calendars     = ParseCalendars(archive);
        var calendarDates = ParseCalendarDates(archive);
        var activeIds     = GetActiveServiceIds(today, calendars, calendarDates);
        logger.LogInformation("Services actifs pour le {Date} : {Count}", today, activeIds.Count);

        // Trajets et horaires : filtrés sur les services actifs du jour → réduction majeure
        var trips = ParseTrips(archive, activeIds);
        var (stopTimes, byStop, byTrip) = ParseStopTimes(archive, trips.Keys.ToHashSet());

        return new DataHolder
        {
            Date              = today,
            Stations          = stations,
            AllStops          = allStops,
            StopsByStation    = stopsByStation,
            Agencies          = agencies,
            Routes            = routes,
            Trips             = trips,
            StopTimes         = stopTimes,
            StopTimesByStopId = byStop,
            StopTimesByTripId = byTrip,
        };
    }

    private async Task<byte[]> DownloadZipAsync(CancellationToken ct)
    {
        var cacheFile = Path.Combine(Path.GetTempPath(), "gtfsdemo_static.zip");
        if (File.Exists(cacheFile) &&
            File.GetLastWriteTimeUtc(cacheFile) > DateTime.UtcNow.AddMinutes(-10))
        {
            logger.LogInformation("Utilisation du cache local : {File}", cacheFile);
            return await File.ReadAllBytesAsync(cacheFile, ct);
        }

        var anonClient = httpClientFactory.CreateClient();
        anonClient.Timeout = TimeSpan.FromSeconds(15);

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", _options.ApiKey);
        client.Timeout = TimeSpan.FromMinutes(5);

        var downloadUrl = await ResolveDownloadUrlAsync(anonClient, _options.GtfsStaticUrl, ct);

        logger.LogInformation("Téléchargement GTFS statique depuis {Url}...", downloadUrl);
        var bytes = await client.GetByteArrayAsync(downloadUrl, ct);
        await File.WriteAllBytesAsync(cacheFile, bytes, ct);
        return bytes;
    }

    private async Task<string> ResolveDownloadUrlAsync(
        HttpClient client, string configuredUrl, CancellationToken ct)
    {
        var match = Regex.Match(configuredUrl, @"/dataset/([^/?#]+)");
        if (!match.Success) return configuredUrl;

        var datasetId = match.Groups[1].Value;
        var baseUri   = new Uri(configuredUrl);
        var apiUrl    = $"{baseUri.Scheme}://{baseUri.Host}/api/3/action/package_show?id={datasetId}";

        logger.LogInformation("Découverte CKAN — dataset : {DatasetId}", datasetId);

        string json;
        try { json = await client.GetStringAsync(apiUrl, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible d'appeler l'API CKAN, URL configurée utilisée");
            return configuredUrl;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            return configuredUrl;

        var resources = root
            .GetProperty("result").GetProperty("resources").EnumerateArray()
            .Where(r =>
            {
                var fmt  = r.TryGetProperty("format", out var f) ? f.GetString() ?? "" : "";
                var name = r.TryGetProperty("name",   out var n) ? n.GetString() ?? "" : "";
                return fmt.Equals("ZIP", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("gtfs", StringComparison.OrdinalIgnoreCase);
            })
            .Select(r =>
            {
                var dateStr = (r.TryGetProperty("last_modified", out var lm) ? lm.GetString() : null)
                           ?? (r.TryGetProperty("created",       out var cr) ? cr.GetString() : null)
                           ?? string.Empty;
                _ = DateTime.TryParse(dateStr, out var date);
                return (resource: r, date);
            })
            .OrderByDescending(x => x.date)
            .ToList();

        if (resources.Count == 0) return configuredUrl;

        var best = resources[0].resource;
        var url  = best.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        if (string.IsNullOrEmpty(url)) return configuredUrl;

        var resourceName = best.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "?";
        logger.LogInformation("Ressource GTFS sélectionnée : {Name} → {Url}", resourceName, url);
        return url;
    }

    // ── Parsers CSV ───────────────────────────────────────────────────────────

    private static CsvConfiguration CsvConfig => new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.ToLowerInvariant(),
        MissingFieldFound     = null,
        HeaderValidated       = null,
        BadDataFound          = null,
    };

    private static (
        Dictionary<string, StationData> stations,
        Dictionary<string, List<string>> stopsByStation,
        Dictionary<string, StopData> allStops)
    ParseStops(ZipArchive archive)
    {
        using var stream = OpenEntry(archive, "stops.txt");
        if (stream is null) return ([], [], []);
        using var reader = new StreamReader(stream);
        using var csv    = new CsvReader(reader, CsvConfig);

        var rows = csv.GetRecords<StopCsvRow>()
             .Where(r => !string.IsNullOrEmpty(r.stop_id))
             .ToList();

        var allStops = rows
            .Where(r => !string.IsNullOrEmpty(r.parent_station))
            .ToDictionary(
                r => string.Intern(r.stop_id),
                r => new StopData(string.Intern(r.stop_id), r.stop_lat, r.stop_lon,
                                  string.Intern(r.parent_station ?? "")));

        var stations = rows
            .Where(r => string.IsNullOrEmpty(r.parent_station))
            .ToDictionary(
                r => string.Intern(r.stop_id),
                r => new StationData(string.Intern(r.stop_id), string.Intern(r.stop_name),
                                     r.stop_lat, r.stop_lon));

        var stopsByStation = stations.ToDictionary(
            r => string.Intern(r.Value.StopId), _ => new List<string>());

        foreach (var stop in allStops.Values)
            stopsByStation[string.Intern(stop.ParentStation)].Add(string.Intern(stop.StopId));

        return (stations, stopsByStation, allStops);
    }

    private static Dictionary<string, AgencyData> ParseAgencies(ZipArchive archive)
    {
        using var stream = OpenEntry(archive, "agency.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv    = new CsvReader(reader, CsvConfig);

        return csv.GetRecords<AgencyCsvRow>()
            .Where(r => !string.IsNullOrEmpty(r.agency_id))
            .ToDictionary(
                r => string.Intern(r.agency_id),
                r => new AgencyData(string.Intern(r.agency_id), r.agency_name));
    }

    private static Dictionary<string, RouteData> ParseRoutes(ZipArchive archive)
    {
        using var stream = OpenEntry(archive, "routes.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv    = new CsvReader(reader, CsvConfig);

        return csv.GetRecords<RouteCsvRow>()
            .Where(r => !string.IsNullOrEmpty(r.route_id))
            .ToDictionary(
                r => string.Intern(r.route_id),
                r => new RouteData(string.Intern(r.route_id), string.Intern(r.route_short_name),
                                   string.Intern(r.route_long_name), string.Intern(r.agency_id)));
    }

    /// <summary>Ne charge que les trajets dont le service est actif aujourd'hui.</summary>
    private static Dictionary<string, TripData> ParseTrips(
        ZipArchive archive, HashSet<string> activeServiceIds)
    {
        using var stream = OpenEntry(archive, "trips.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv    = new CsvReader(reader, CsvConfig);

        return csv.GetRecords<TripCsvRow>()
            .Where(r => !string.IsNullOrEmpty(r.trip_id)
                     && activeServiceIds.Contains(r.service_id))
            .ToDictionary(
                r => string.Intern(r.trip_id),
                r => new TripData(string.Intern(r.trip_id), string.Intern(r.route_id),
                                  r.service_id, r.trip_headsign ?? ""));
    }

    private static (
        Dictionary<int, StopTimeData> stopTimes,
        Dictionary<string, List<int>> byStop,
        Dictionary<string, List<int>> byTrip)
    ParseStopTimes(ZipArchive archive, HashSet<string> validTripIds)
    {
        using var stream = OpenEntry(archive, "stop_times.txt");
        if (stream is null) return ([], [], []);
        using var reader = new StreamReader(stream);
        using var csv    = new CsvReader(reader, CsvConfig);

        var result = new Dictionary<int, StopTimeData>();
        var byStop = new Dictionary<string, List<int>>();
        var byTrip = new Dictionary<string, List<int>>();
        int id = 1;

        foreach (var row in csv.GetRecords<StopTimeCsvRow>())
        {
            if (!validTripIds.Contains(row.trip_id)) continue;
            if (!TryParseGtfsTime(row.departure_time, out var dep)) continue;
            if (!TryParseGtfsTime(row.arrival_time,   out var arr)) continue;

            var st = new StopTimeData(string.Intern(row.trip_id), string.Intern(row.stop_id),
                                      arr, dep, row.stop_sequence);
            result[id] = st;

            if (!byStop.TryGetValue(row.stop_id, out var sl)) byStop[row.stop_id] = sl = [];
            sl.Add(id);
            if (!byTrip.TryGetValue(row.trip_id, out var tl)) byTrip[row.trip_id] = tl = [];
            tl.Add(id);
            id++;
        }
        return (result, byStop, byTrip);
    }

    private static Dictionary<string, CalendarData> ParseCalendars(ZipArchive archive)
    {
        using var stream = OpenEntry(archive, "calendar.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv    = new CsvReader(reader, CsvConfig);

        return csv.GetRecords<CalendarCsvRow>()
            .Where(r => !string.IsNullOrEmpty(r.service_id))
            .ToDictionary(
                r => r.service_id,
                r => new CalendarData(
                    r.service_id,
                    [r.monday == 1, r.tuesday == 1, r.wednesday == 1,
                     r.thursday == 1, r.friday == 1, r.saturday == 1, r.sunday == 1],
                    ParseGtfsDate(r.start_date),
                    ParseGtfsDate(r.end_date)));
    }

    private static Dictionary<string, List<CalendarDateData>> ParseCalendarDates(ZipArchive archive)
    {
        using var stream = OpenEntry(archive, "calendar_dates.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv    = new CsvReader(reader, CsvConfig);

        var index = new Dictionary<string, List<CalendarDateData>>();
        foreach (var row in csv.GetRecords<CalendarDateCsvRow>())
        {
            if (!index.TryGetValue(row.service_id, out var list))
                index[row.service_id] = list = [];
            list.Add(new CalendarDateData(row.service_id, ParseGtfsDate(row.date), row.exception_type));
        }
        return index;
    }

    // ── Calendrier ────────────────────────────────────────────────────────────

    private static HashSet<string> GetActiveServiceIds(
        DateOnly date,
        Dictionary<string, CalendarData> calendars,
        Dictionary<string, List<CalendarDateData>> calendarDates)
    {
        var active = new HashSet<string>();

        // DayOfWeek .NET : 0=Dimanche … 6=Samedi → index GTFS : 0=Lundi … 6=Dimanche
        var dotNetDay = (int)date.DayOfWeek;
        var gtfsDay   = dotNetDay == 0 ? 6 : dotNetDay - 1;

        foreach (var (sid, cal) in calendars)
            if (date >= cal.StartDate && date <= cal.EndDate && cal.DaysOfWeek[gtfsDay])
                active.Add(sid);

        foreach (var (sid, exceptions) in calendarDates)
            foreach (var ex in exceptions.Where(e => e.Date == date))
            {
                if (ex.ExceptionType == 1) active.Add(sid);
                else if (ex.ExceptionType == 2) active.Remove(sid);
            }

        return active;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Stream? OpenEntry(ZipArchive archive, string filename)
    {
        var entry = archive.GetEntry(filename)
            ?? archive.Entries.FirstOrDefault(e =>
                e.Name.Equals(filename, StringComparison.OrdinalIgnoreCase));
        return entry?.Open();
    }

    private static bool TryParseGtfsTime(string raw, out TimeSpan result)
    {
        result = default;
        if (string.IsNullOrEmpty(raw)) return false;
        var parts = raw.Split(':');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var h)) return false;
        if (!int.TryParse(parts[1], out var m)) return false;
        if (!int.TryParse(parts[2], out var s)) return false;
        result = new TimeSpan(h, m, s);
        return true;
    }

    private static DateOnly ParseGtfsDate(string raw) =>
        DateOnly.ParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture);

    private static TimeSpan NormalizeTime(TimeSpan t) =>
        t.TotalHours >= 24 ? t - TimeSpan.FromHours(24) : t;

    private static DateTime BuildDepartureDateTime(DateOnly date, TimeSpan gtfsTime)
    {
        if (gtfsTime.TotalHours >= 24)
            return date.AddDays(1).ToDateTime(TimeOnly.MinValue)
                       .Add(gtfsTime - TimeSpan.FromHours(24));
        return date.ToDateTime(TimeOnly.MinValue).Add(gtfsTime);
    }

    private static Station ToModel(StationData s) =>
        new() { StopId = s.StopId, Name = s.Name, Lat = s.Lat, Lon = s.Lon };

    // ── Snapshot de données ───────────────────────────────────────────────────

    private sealed class DataHolder
    {
        public required DateOnly Date { get; init; }
        public required Dictionary<string, StationData> Stations { get; init; }
        public required Dictionary<string, StopData> AllStops { get; init; }
        public required Dictionary<string, List<string>> StopsByStation { get; init; }
        public required Dictionary<string, AgencyData> Agencies { get; init; }
        public required Dictionary<string, RouteData> Routes { get; init; }
        public required Dictionary<string, TripData> Trips { get; init; }
        public required Dictionary<int, StopTimeData> StopTimes { get; init; }
        public required Dictionary<string, List<int>> StopTimesByStopId { get; init; }
        public required Dictionary<string, List<int>> StopTimesByTripId { get; init; }
    }

    // ── Records internes ──────────────────────────────────────────────────────

    private sealed record StopData(string StopId, double Lat, double Lon, string ParentStation);
    private sealed record StationData(string StopId, string Name, double Lat, double Lon);
    private sealed record RouteData(string RouteId, string ShortName, string LongName, string AgencyId);
    private sealed record AgencyData(string AgencyId, string Name);
    private sealed record TripData(string TripId, string RouteId, string ServiceId, string Headsign);
    private sealed record StopTimeData(string TripId, string StopId, TimeSpan ArrivalTime, TimeSpan DepartureTime, int StopSequence);
    private sealed record CalendarData(string ServiceId, bool[] DaysOfWeek, DateOnly StartDate, DateOnly EndDate);
    private sealed record CalendarDateData(string ServiceId, DateOnly Date, int ExceptionType);

    // ── Classes CSV ───────────────────────────────────────────────────────────

    private class StopCsvRow
    {
        public string stop_id { get; set; } = "";
        public string stop_name { get; set; } = "";
        public double stop_lat { get; set; }
        public double stop_lon { get; set; }
        public string? parent_station { get; set; }
    }

    private class RouteCsvRow
    {
        public string route_id { get; set; } = "";
        public string route_short_name { get; set; } = "";
        public string route_long_name { get; set; } = "";
        public string agency_id { get; set; } = "";
    }

    private class AgencyCsvRow
    {
        public string agency_id { get; set; } = "";
        public string agency_name { get; set; } = "";
    }

    private class TripCsvRow
    {
        public string trip_id { get; set; } = "";
        public string route_id { get; set; } = "";
        public string service_id { get; set; } = "";
        public string? trip_headsign { get; set; }
    }

    private class StopTimeCsvRow
    {
        public string trip_id { get; set; } = "";
        public string stop_id { get; set; } = "";
        public string arrival_time { get; set; } = "";
        public string departure_time { get; set; } = "";
        public int stop_sequence { get; set; }
    }

    private class CalendarCsvRow
    {
        public string service_id { get; set; } = "";
        public int monday { get; set; }
        public int tuesday { get; set; }
        public int wednesday { get; set; }
        public int thursday { get; set; }
        public int friday { get; set; }
        public int saturday { get; set; }
        public int sunday { get; set; }
        public string start_date { get; set; } = "";
        public string end_date { get; set; } = "";
    }

    private class CalendarDateCsvRow
    {
        public string service_id { get; set; } = "";
        public string date { get; set; } = "";
        public int exception_type { get; set; }
    }
}
