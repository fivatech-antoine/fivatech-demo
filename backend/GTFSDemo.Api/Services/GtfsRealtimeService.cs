using GTFSDemo.Api.Configuration;
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

    // tripId → délai en secondes (swap atomique lors du refresh)
    private volatile Dictionary<string, int> _tripDelays = [];

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

    public int? GetTripDelaySeconds(string tripId) =>
        _tripDelays.TryGetValue(tripId, out var d) ? d : null;

    // ── Refresh ───────────────────────────────────────────────────────────────

    private async Task RefreshAsync(CancellationToken ct)
    {
        var feed = await FetchFeedAsync(ct);
        if (feed is null) return;

        int totalEntities   = feed.Entity.Count;
        int tripUpdateCount = feed.Entity.Count(e => e.TripUpdate != null);

        var newDelays = ParseTripDelays(feed);
        _tripDelays = newDelays;

        logger.LogInformation(
            "GTFS-RT : {Total} entités, {TU} TripUpdates, {Delays} avec délai",
            totalEntities, tripUpdateCount, newDelays.Count);
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
            // Note : Accept-Encoding est géré automatiquement par AutomaticDecompression
            // sur le client "gtfsrt" — ne pas le redéfinir manuellement.

            var client = httpClientFactory.CreateClient("gtfsrt");
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            logger.LogInformation("GTFS-RT : {Bytes} octets reçus (HTTP {Status})",
                bytes.Length, (int)response.StatusCode);

            return ProtoFeed.Parser.ParseFrom(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de récupérer le flux GTFS-RT : {Url}", _options.GtfsRtUrl);
            return null;
        }
    }

    // ── Parsers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, int> ParseTripDelays(ProtoFeed feed)
    {
        var delays = new Dictionary<string, int>();

        foreach (var entity in feed.Entity)
        {
            if (entity.TripUpdate is not { } tu) continue;
            if (tu.Trip is not { } trip || string.IsNullOrEmpty(trip.TripId)) continue;

            // Priorité 1 : délai global sur le TripUpdate (rare mais direct)
            if (tu.HasDelay)
            {
                delays[trip.TripId] = tu.Delay;
                continue;
            }

            // Priorité 2 : dernier StopTimeUpdate avec délai (départ ou arrivée).
            // On prend le DERNIER car la liste est ordonnée par séquence d'arrêt :
            // le dernier arrêt connu reflète le mieux l'état actuel du train.
            // On vérifie Departure en priorité, puis Arrival en fallback
            // (certains fournisseurs ne renseignent que l'arrivée au terminus).
            int? delay = null;
            foreach (var stu in tu.StopTimeUpdate)
            {
                if (stu.Departure?.HasDelay == true)
                    delay = stu.Departure.Delay;
                else if (stu.Arrival?.HasDelay == true)
                    delay = stu.Arrival.Delay;
            }

            if (delay.HasValue)
                delays[trip.TripId] = delay.Value;
        }

        return delays;
    }
}
