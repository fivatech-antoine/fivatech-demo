using GTFSDemo.Api.Configuration;
using GTFSDemo.Api.Models;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using ProtoFeed = GTFSDemo.Api.Protos.FeedMessage;

namespace GTFSDemo.Api.Services;

/// <summary>
/// Consomme le flux GTFS-RT unique toutes les 30 s.
/// Un seul GET sur /la/gtfs-rt retourne vehicles + trip-updates dans le même FeedMessage.
/// </summary>
public class GtfsRealtimeService(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenTransportDataOptions> options,
    GtfsStaticService staticService,
    ILogger<GtfsRealtimeService> logger) : BackgroundService
{
    private readonly OpenTransportDataOptions _options = options.Value;

    // 30 s = 2 req/min → bien en dessous du quota de 5 req/min
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    // ── Données temps réel en mémoire ─────────────────────────────────────────
    private IReadOnlyList<VehiclePosition> _vehiclePositions = [];
    // tripId → délai global en secondes
    private Dictionary<string, int> _tripDelays = [];

    // ── BackgroundService ─────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Attendre que le GTFS statique soit chargé avant la première récupération
        while (!staticService.IsLoaded && !stoppingToken.IsCancellationRequested)
            await Task.Delay(500, stoppingToken);

        // Délai initial de 5 s pour ne pas cumuler avec le téléchargement du ZIP statique
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Erreur lors du refresh GTFS-RT");
            }

            await Task.Delay(RefreshInterval, stoppingToken);
        }
    }

    // ── API publique ──────────────────────────────────────────────────────────

    public IReadOnlyList<VehiclePosition> GetVehiclePositions() => _vehiclePositions;

    public int? GetTripDelaySeconds(string tripId) =>
        _tripDelays.TryGetValue(tripId, out var d) ? d : null;

    // ── Refresh ───────────────────────────────────────────────────────────────

    private async Task RefreshAsync(CancellationToken ct)
    {
        var feed = await FetchFeedAsync(ct);
        if (feed is null) return;

        _vehiclePositions = ParseVehiclePositions(feed);
        _tripDelays = ParseTripDelays(feed);

        logger.LogDebug(
            "GTFS-RT mis à jour : {Vehicles} véhicules, {Trips} trajets avec données",
            _vehiclePositions.Count, _tripDelays.Count);
    }

    private async Task<ProtoFeed?> FetchFeedAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.GtfsRtUrl)) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _options.GtfsRtUrl);
            // Auth : clé brute avec préfixe Bearer (cf. documentation opentransportdata.swiss)
            request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
            request.Headers.Add("User-Agent", "gtfsdemo/1.0");
            // Compression : réduit la taille d'~90 % selon la doc
            request.Headers.Add("Accept-Encoding", "gzip, deflate");

            var client = httpClientFactory.CreateClient("gtfsrt");
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return ProtoFeed.Parser.ParseFrom(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de récupérer le flux GTFS-RT : {Url}", _options.GtfsRtUrl);
            return null;
        }
    }

    // ── Parsers ───────────────────────────────────────────────────────────────

    private List<VehiclePosition> ParseVehiclePositions(ProtoFeed feed)
    {
        var result = new List<VehiclePosition>();

        foreach (var entity in feed.Entity)
        {
            if (entity.Vehicle is not { } v) continue;
            if (v.Position is not { } pos) continue;

            var tripId = v.Trip?.TripId ?? "";
            var routeShortName = !string.IsNullOrEmpty(tripId)
                ? staticService.GetRouteShortNameForTrip(tripId)
                : (v.Trip?.RouteId ?? "");

            result.Add(new VehiclePosition
            {
                VehicleId = v.Vehicle?.Id ?? entity.Id,
                TripId = tripId,
                RouteShortName = routeShortName,
                Latitude = pos.Latitude,
                Longitude = pos.Longitude,
                Bearing = pos.HasBearing ? pos.Bearing : null,
                Speed = pos.HasSpeed ? pos.Speed : null,
                Timestamp = v.HasTimestamp
                    ? DateTimeOffset.FromUnixTimeSeconds((long)v.Timestamp).UtcDateTime
                    : DateTime.UtcNow,
                CurrentStatus = v.CurrentStatus.ToString(),
            });
        }

        return result;
    }

    private static Dictionary<string, int> ParseTripDelays(ProtoFeed feed)
    {
        var delays = new Dictionary<string, int>();

        foreach (var entity in feed.Entity)
        {
            if (entity.TripUpdate is not { } tu) continue;
            if (tu.Trip is not { } trip || string.IsNullOrEmpty(trip.TripId)) continue;

            if (tu.HasDelay)
            {
                delays[trip.TripId] = tu.Delay;
                continue;
            }

            // Délai du premier StopTimeUpdate disponible
            var firstDelay = tu.StopTimeUpdate
                .Where(stu => stu.Departure?.HasDelay == true)
                .Select(stu => (int?)stu.Departure!.Delay)
                .FirstOrDefault();

            if (firstDelay.HasValue)
                delays[trip.TripId] = firstDelay.Value;
        }

        return delays;
    }
}
