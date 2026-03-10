using System.Collections;
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
/// Charge et indexe le GTFS statique (ZIP) au démarrage de l'application.
/// Fournit des méthodes de recherche d'arrêts et de calcul des départs théoriques.
/// </summary>
public class GtfsStaticService(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenTransportDataOptions> options,
    ILogger<GtfsStaticService> logger) : IHostedService
{
    private readonly OpenTransportDataOptions _options = options.Value;

    // ── Index en mémoire ─────────────────────────────────────────────────────
    private Dictionary<string, StationData> _stations = [];
    private Dictionary<string, StopData> _allStops = [];          // y compris les quais
    private Dictionary<string, List<string>> _stopsByStation = [];
    private Dictionary<string, AgencyData> _agencies = [];
    private Dictionary<string, RouteData> _routes = [];
    private Dictionary<string, TripData> _trips = [];
    private Dictionary<int, StopTimeData> _stopTimes = [];
    private Dictionary<string, List<int>> _stopTimesByStopId = [];
    private Dictionary<string, List<int>> _stopTimesByTripId = [];
    private Dictionary<string, CalendarData> _calendars = [];
    private Dictionary<string, List<CalendarDateData>> _calendarDates = [];

    public bool IsLoaded { get; private set; }

    // ── IHostedService ───────────────────────────────────────────────────────
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Chargement du GTFS statique depuis {Url}", _options.GtfsStaticUrl);
        try
        {
            await LoadAsync(cancellationToken);
            IsLoaded = true;
            logger.LogInformation(
                "GTFS statique chargé : {Stations} stations, {Stops} stops, {Routes} lignes, {Trips} trajets",
                _stations.Count, _allStops.Count, _routes.Count, _trips.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec du chargement GTFS statique");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ── API publique ─────────────────────────────────────────────────────────

    public IReadOnlyList<Station> SearchStops(string query, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        return _stations.Values
            .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name)
            .Take(limit)
            .Select(ToModel)
            .ToList();
    }

    public Station? GetStop(string stopId) =>
        _stations.TryGetValue(stopId, out var s) ? ToModel(s) : null;

    public IReadOnlyList<Departure> GetDepartures(string stopId, int limit = 20)
    {
        List<StopTimeData> stopTimes = new List<StopTimeData>();
        if (_stopTimesByStopId.TryGetValue(stopId, out var times))
        {
            foreach (int stId in times)
            {
                stopTimes.AddRange(_stopTimes[stId]);
            }
        }
        foreach (string platformId in _stopsByStation[stopId])
        {
            if (_stopTimesByStopId.TryGetValue(platformId, out var ptTimes))
            {
                foreach (int stId in ptTimes)
                {
                    stopTimes.AddRange(_stopTimes[stId]);
                }
            }
        }
        if (stopTimes.Count == 0)
            return [];

        var today = DateOnly.FromDateTime(DateTime.Now);
        var activeServices = GetActiveServiceIds(today);
        var nowTimeOfDay = DateTime.Now.TimeOfDay - TimeSpan.FromMinutes(1); // 1 min de grâce

        return stopTimes
            .Where(st =>
            {
                if (!_trips.TryGetValue(st.TripId, out var trip)) return false;
                if (!activeServices.Contains(trip.ServiceId)) return false;
                if (_stopTimesByTripId.TryGetValue(trip.TripId, out var stopIdsOfTheTrip))
                {
                    List<StopTimeData> stopsOfTheTrip = new List<StopTimeData>();
                    foreach (int id in stopIdsOfTheTrip)
                    {
                        stopsOfTheTrip.Add(_stopTimes[id]);
                    }
                    int maxSequence = stopsOfTheTrip.Max(s => s.StopSequence);
                    StopTimeData? lastStop = stopsOfTheTrip.FirstOrDefault(st => st.StopSequence == maxSequence);
                    if (lastStop != null && _stopsByStation[stopId].Contains(lastStop.StopId))
                        return false;
                }
                // Normalise les heures > 24h (services passant minuit)
                //var normalizedTime = NormalizeTime(st.DepartureTime);
                return st.DepartureTime >= nowTimeOfDay;
            })
            .OrderBy(st => NormalizeTime(st.DepartureTime))
            .Take(limit)
            .Select(st =>
            {
                _trips.TryGetValue(st.TripId, out var trip);
                _routes.TryGetValue(trip?.RouteId ?? "", out var route);
                _agencies.TryGetValue(route?.AgencyId ?? "", out var agency);
                var scheduled = BuildDepartureDateTime(today, st.DepartureTime);
                return new Departure
                {
                    TripId = st.TripId,
                    RouteShortName = route?.ShortName ?? "",
                    Headsign = trip?.Headsign ?? "",
                    ScheduledDeparture = scheduled,
                    StopId = stopId,
                    Operator = agency?.Name
                };
            })
            .ToList();
    }

    public string GetRouteShortNameForTrip(string tripId)
    {
        if (!_trips.TryGetValue(tripId, out var trip)) return "";
        if (!_routes.TryGetValue(trip.RouteId, out var route)) return "";
        return route.ShortName;
    }

    /// <summary>
    /// Retourne les arrêts restants d'un trajet à partir d'un arrêt donné (inclus).
    /// Si <paramref name="fromStopId"/> est vide, retourne tous les arrêts du trajet.
    /// </summary>
    public IReadOnlyList<TripStop> GetTripRemainingStops(string tripId, string fromStopId)
    {
        if (!_stopTimesByTripId.TryGetValue(tripId, out var stopTimeIds))
            return [];
        
        List<StopTimeData> stopTimes = new List<StopTimeData>();
        foreach (int id in stopTimeIds)
        {
            stopTimes.Add(_stopTimes[id]);
        }
        var sorted = stopTimes.OrderBy(st => st.StopSequence).ToList();

        int fromSeq = 0;
        if (!string.IsNullOrEmpty(fromStopId))
        {
            // Essai direct sur l'ID de l'arrêt
            var match = sorted.FirstOrDefault(st => st.StopId == fromStopId);
            if (match == null)
            {
                // L'arrêt passé est peut-être un arrêt-parent : chercher parmi ses quais
                var childIds = _stopsByStation.TryGetValue(fromStopId, out var children)
                    ? children.ToHashSet()
                    : [];
                match = sorted.FirstOrDefault(st => childIds.Contains(st.StopId));
            }
            if (match != null)
                fromSeq = match.StopSequence;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        return sorted
            .Where(st => st.StopSequence >= fromSeq)
            .Select(st =>
            {
                StationData? station = null;
                if (_allStops.TryGetValue(st.StopId, out var stop))
                    _stations.TryGetValue(stop.ParentStation, out station);
                return new TripStop
                {
                    StopId = st.StopId,
                    Name = station?.Name ?? st.StopId,
                    Lat = stop?.Lat ?? 0,
                    Lon = stop?.Lon ?? 0,
                    Sequence = st.StopSequence,
                    ScheduledDeparture = BuildDepartureDateTime(today, st.DepartureTime),
                };
            })
            .ToList();
    }

    // ── Chargement GTFS ──────────────────────────────────────────────────────

    private async Task LoadAsync(CancellationToken ct)
    {
        var zipBytes = await DownloadZipAsync(ct);
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        _stations = ParseStops(archive, out _stopsByStation, out _allStops);
        _routes = ParseRoutes(archive);
        _agencies = ParseAgencies(archive);
        _trips = ParseTrips(archive);
        _calendars = ParseCalendars(archive);
        _calendarDates = ParseCalendarDates(archive);
        _stopTimes = ParseStopTimes(archive, _trips.Keys.ToHashSet(), out _stopTimesByStopId, out _stopTimesByTripId);
    }

    private async Task<byte[]> DownloadZipAsync(CancellationToken ct)
    {
        // Cache local 12 h — évite de re-télécharger à chaque restart dev
        var cacheFile = Path.Combine(Path.GetTempPath(), "gtfsdemo_static.zip");
        if (File.Exists(cacheFile) &&
            File.GetLastWriteTimeUtc(cacheFile) > DateTime.UtcNow.AddMinutes(-10))
        {
            logger.LogInformation("Utilisation du cache local : {File}", cacheFile);
            return await File.ReadAllBytesAsync(cacheFile, ct);
        }

        // Client sans auth pour la découverte CKAN (API publique)
        var anonClient = httpClientFactory.CreateClient();
        anonClient.Timeout = TimeSpan.FromSeconds(15);

        // Client authentifié pour le téléchargement du fichier
        var client = httpClientFactory.CreateClient();
        // opentransportdata.swiss attend la clé brute, sans préfixe "Bearer"
        client.DefaultRequestHeaders.Add("Authorization", _options.ApiKey);
        client.Timeout = TimeSpan.FromMinutes(5);

        // Résolution automatique si l'URL pointe vers une page de dataset CKAN
        var downloadUrl = await ResolveDownloadUrlAsync(anonClient, _options.GtfsStaticUrl, ct);

        logger.LogInformation("Téléchargement GTFS statique depuis {Url}...", downloadUrl);
        var bytes = await client.GetByteArrayAsync(downloadUrl, ct);
        await File.WriteAllBytesAsync(cacheFile, bytes, ct);
        return bytes;
    }

    /// <summary>
    /// Si l'URL configurée contient /dataset/{id}, interroge l'API CKAN pour
    /// trouver l'URL de téléchargement du ZIP le plus récent.
    /// Sinon retourne l'URL telle quelle.
    /// </summary>
    private async Task<string> ResolveDownloadUrlAsync(
        HttpClient client, string configuredUrl, CancellationToken ct)
    {
        // Détection du pattern CKAN : .../dataset/{dataset-id}...
        var match = Regex.Match(configuredUrl, @"/dataset/([^/?#]+)");
        if (!match.Success)
            return configuredUrl; // URL directe — utiliser telle quelle

        var datasetId = match.Groups[1].Value;
        var baseUri = new Uri(configuredUrl);
        var apiUrl = $"{baseUri.Scheme}://{baseUri.Host}/api/3/action/package_show?id={datasetId}";

        logger.LogInformation("Découverte CKAN — dataset : {DatasetId}, API : {ApiUrl}", datasetId, apiUrl);

        string json;
        try
        {
            json = await client.GetStringAsync(apiUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible d'appeler l'API CKAN, utilisation de l'URL configurée");
            return configuredUrl;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
        {
            logger.LogWarning("L'API CKAN a retourné success=false pour {DatasetId}", datasetId);
            return configuredUrl;
        }

        // Filtrer les ressources ZIP et choisir la plus récente
        var resources = root
            .GetProperty("result")
            .GetProperty("resources")
            .EnumerateArray()
            .Where(r =>
            {
                var fmt  = r.TryGetProperty("format", out var f) ? f.GetString() ?? "" : "";
                var name = r.TryGetProperty("name",   out var n) ? n.GetString() ?? "" : "";
                return fmt.Equals("ZIP", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("gtfs", StringComparison.OrdinalIgnoreCase);
            })
            .Select(r =>
            {
                // last_modified est prioritaire sur created
                var dateStr = (r.TryGetProperty("last_modified", out var lm) ? lm.GetString() : null)
                           ?? (r.TryGetProperty("created",       out var cr) ? cr.GetString() : null)
                           ?? string.Empty;
                _ = DateTime.TryParse(dateStr, out var date);
                return (resource: r, date);
            })
            .OrderByDescending(x => x.date)
            .ToList();

        if (resources.Count == 0)
        {
            logger.LogWarning("Aucune ressource ZIP trouvée pour le dataset {DatasetId}", datasetId);
            return configuredUrl;
        }

        var best = resources[0].resource;
        var url = best.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        if (string.IsNullOrEmpty(url))
        {
            logger.LogWarning("Ressource sélectionnée sans URL pour {DatasetId}", datasetId);
            return configuredUrl;
        }

        var resourceName = best.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "?";
        logger.LogInformation("Ressource GTFS sélectionnée : {Name} → {Url}", resourceName, url);
        return url;
    }

    // ── Parsers CSV ───────────────────────────────────────────────────────────

    private static CsvConfiguration CsvConfig => new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.ToLowerInvariant(),
        MissingFieldFound = null,
        HeaderValidated = null,
        BadDataFound = null,
    };

    private static Dictionary<string, StationData> ParseStops(
        ZipArchive archive,
        out Dictionary<string, List<string>> stopsByStation,
        out Dictionary<string, StopData> allStops)
    {
        using var stream = OpenEntry(archive, "stops.txt");
        stopsByStation = [];
        allStops = [];
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CsvConfig);
        var rows = csv.GetRecords<StopCsvRow>()
             .Where(r => !string.IsNullOrEmpty(r.stop_id))
             .ToList();

        // Index des quais
        allStops = rows
            .Where(r => !string.IsNullOrEmpty(r.parent_station))
            .ToDictionary(
                r => string.Intern(r.stop_id),
                r => new StopData(string.Intern(r.stop_id), r.stop_lat, r.stop_lon, string.Intern(r.parent_station ?? "")));

        // Liste les stations (pas de parent) — utilisé pour la recherche
        Dictionary<string, StationData> result = rows
            .Where(r => string.IsNullOrEmpty(r.parent_station))
            .ToDictionary(
                r => string.Intern(r.stop_id),
                r => new StationData(string.Intern(r.stop_id), string.Intern(r.stop_name), r.stop_lat, r.stop_lon));

        // Initialise la liste des stops par stations
        stopsByStation = result.ToDictionary(r => string.Intern(r.Value.StopId), r => new List<string>());

        foreach(StopData stopData in allStops.Values)
        {
             stopsByStation[string.Intern(stopData.ParentStation)].Add(string.Intern(stopData.StopId));
        }

        return result;
    }
    private static Dictionary<string, AgencyData> ParseAgencies(ZipArchive archive)
    {
        using var stream = OpenEntry(archive, "agency.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CsvConfig);

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
        using var csv = new CsvReader(reader, CsvConfig);

        return csv.GetRecords<RouteCsvRow>()
            .Where(r => !string.IsNullOrEmpty(r.route_id))
            .ToDictionary(
                r => string.Intern(r.route_id),
                r => new RouteData(string.Intern(r.route_id), string.Intern(r.route_short_name), string.Intern(r.route_long_name), string.Intern(r.agency_id)));
    }

    private static Dictionary<string, TripData> ParseTrips(ZipArchive archive)
    {
        using var stream = OpenEntry(archive, "trips.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CsvConfig);

        return csv.GetRecords<TripCsvRow>()
            .Where(r => !string.IsNullOrEmpty(r.trip_id))
            .ToDictionary(
                r => string.Intern(r.trip_id),
                r => new TripData(string.Intern(r.trip_id), string.Intern(r.route_id), r.service_id, r.trip_headsign ?? ""));
    }

    private static Dictionary<int, StopTimeData> ParseStopTimes(
        ZipArchive archive, HashSet<string> validTripIds,
        out Dictionary<string, List<int>> byStop,
        out Dictionary<string, List<int>> byTrip)
    {
        using var stream = OpenEntry(archive, "stop_times.txt");
        byStop = [];
        byTrip = [];
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CsvConfig);

        var result = new Dictionary<int, StopTimeData>();
        int id = 1;
        foreach (var row in csv.GetRecords<StopTimeCsvRow>())
        {
            if (!validTripIds.Contains(row.trip_id)) continue;
            if (!TryParseGtfsTime(row.departure_time, out var depTime)) continue;
            if (!TryParseGtfsTime(row.arrival_time, out var arrTime)) continue;

            var st = new StopTimeData(string.Intern(row.trip_id), string.Intern(row.stop_id), arrTime, depTime, row.stop_sequence);

            result.Add(id, st);

            if (!byStop.TryGetValue(row.stop_id, out var stopList))
            {
                stopList = [];
                byStop[row.stop_id] = stopList;
            }
            stopList.Add(id);

            if (!byTrip.TryGetValue(row.trip_id, out var tripList))
            {
                tripList = [];
                byTrip[row.trip_id] = tripList;
            }
            tripList.Add(id);
            id++;
        }
        return result;
    }

    private static Dictionary<string, CalendarData> ParseCalendars(ZipArchive archive)
    {
        using var stream = OpenEntry(archive, "calendar.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CsvConfig);

        return csv.GetRecords<CalendarCsvRow>()
            .Where(r => !string.IsNullOrEmpty(r.service_id))
            .ToDictionary(
                r => r.service_id,
                r => new CalendarData(
                    r.service_id,
                    // Ordre GTFS : lundi=0 ... dimanche=6
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
        using var csv = new CsvReader(reader, CsvConfig);

        var index = new Dictionary<string, List<CalendarDateData>>();
        foreach (var row in csv.GetRecords<CalendarDateCsvRow>())
        {
            if (!index.TryGetValue(row.service_id, out var list))
            {
                list = [];
                index[row.service_id] = list;
            }
            list.Add(new CalendarDateData(row.service_id, ParseGtfsDate(row.date), row.exception_type));
        }
        return index;
    }

    // ── Calendrier ────────────────────────────────────────────────────────────

    private HashSet<string> GetActiveServiceIds(DateOnly date)
    {
        var active = new HashSet<string>();

        // DayOfWeek : 0=Dimanche, 1=Lundi … 6=Samedi
        // Index GTFS : 0=Lundi … 6=Dimanche
        var dotNetDay = (int)date.DayOfWeek;
        var gtfsDay = dotNetDay == 0 ? 6 : dotNetDay - 1;

        foreach (var (sid, cal) in _calendars)
        {
            if (date >= cal.StartDate && date <= cal.EndDate && cal.DaysOfWeek[gtfsDay])
                active.Add(sid);
        }

        foreach (var (sid, exceptions) in _calendarDates)
        {
            foreach (var ex in exceptions.Where(e => e.Date == date))
            {
                if (ex.ExceptionType == 1) active.Add(sid);
                else if (ex.ExceptionType == 2) active.Remove(sid);
            }
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

    /// <summary>Normalise les temps GTFS > 24h (services passant minuit).</summary>
    private static TimeSpan NormalizeTime(TimeSpan t) =>
        t.TotalHours >= 24 ? t - TimeSpan.FromHours(24) : t;

    private static DateTime BuildDepartureDateTime(DateOnly date, TimeSpan gtfsTime)
    {
        if (gtfsTime.TotalHours >= 24)
            return date.AddDays(1).ToDateTime(TimeOnly.MinValue)
                   .Add(gtfsTime - TimeSpan.FromHours(24));
        return date.ToDateTime(TimeOnly.MinValue).Add(gtfsTime);
    }

    private static Station ToModel(StationData s) => new()
    {
        StopId = s.StopId,
        Name = s.Name,
        Lat = s.Lat,
        Lon = s.Lon,
    };

    // ── Records internes ──────────────────────────────────────────────────────

    private sealed record StopData(string StopId, double Lat, double Lon, string ParentStation);
    private sealed record StationData(string StopId, string Name, double Lat, double Lon);
    private sealed record RouteData(string RouteId, string ShortName, string LongName, string AgencyId);
    private sealed record AgencyData(string AgencyId, string Name);
    private sealed record TripData(string TripId, string RouteId, string ServiceId, string Headsign);
    private sealed record StopTimeData(string TripId, string StopId, TimeSpan ArrivalTime, TimeSpan DepartureTime, int StopSequence);
    private sealed record CalendarData(string ServiceId, bool[] DaysOfWeek, DateOnly StartDate, DateOnly EndDate);
    private sealed record CalendarDateData(string ServiceId, DateOnly Date, int ExceptionType);

    // ── Classes CSV (noms en snake_case = colonnes GTFS) ─────────────────────

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
