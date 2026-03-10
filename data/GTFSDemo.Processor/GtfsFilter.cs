using GTFSDemo.Processor.Models;

namespace GTFSDemo.Processor;

/// <summary>
/// Filtre les données d'un flux GTFS de manière cohérente (filtrage en cascade).
/// Configurer les propriétés puis appeler <see cref="Apply"/> pour obtenir le flux filtré.
/// </summary>
public class GtfsFilter
{

    /// <summary>
    /// Applique les filtres configurés et retourne un <see cref="GtfsFeed"/> filtré.
    /// Le filtrage est cohérent en cascade :
    /// agences → lignes → trajets → horaires → arrêts utilisés → calendriers → transferts → fréquences.
    /// Si aucun filtre n'est configuré, le flux d'entrée est retourné tel quel (passthrough).
    /// </summary>
    public GtfsFeed Apply(GtfsFeed feed)
    {
        // Simplifier agences
        foreach (Agency agency in feed.Agencies.Values)
        {
            agency.Lang = "";
            agency.Phone = "";
            agency.Timezone = "";
            agency.Url = "";
        }

        // Filtre géographiquement les routes
        foreach (Route route in feed.Routes.Values)
        {
            route.CalculateBox(46.2, 46.6, 6, 7);
        }
        foreach (Agency agency in feed.Agencies.Values)
        {
            agency.IsInTheBox = agency.Routes.Where(r => r.IsInTheBox).ToList().Count > 0;
        }

        var agencies = feed.Agencies
            .Where(a => a.Value.IsInTheBox)
            .ToDictionary();

        var agencyIds = agencies.Keys;

        // Lignes (filtrées par agence)
        var routes = feed.Routes
            .Where(r => agencyIds.Contains(r.Value.AgencyId) && r.Value.IsInTheBox)
            .ToDictionary();
        var routeIds = routes.Keys;

        // Trajets (filtrés par ligne)
        var trips = feed.Trips.Where(t => routeIds.Contains(t.Value.RouteId) && t.Value.IsInTheBox).ToDictionary();
        foreach (Trip trip in trips.Values)
        {
            trip.OriginalTripId = "";
        }
        var tripIds = trips.Keys;
        var serviceIds = trips.Select(t => t.Value.ServiceId).ToHashSet();

        // Horaires (filtrés par trajet)
        var stopTimes = feed.StopTimes.Where(st => tripIds.Contains(st.Value.TripId)).ToDictionary();
        var usedStopIds = stopTimes.Select(st => st.Value.StopId).ToHashSet();

        // Arrêts (utilisés dans les horaires + leurs parents)
        var parentIds = feed.Stops
            .Where(s => usedStopIds.Contains(s.Value.StopId) && !string.IsNullOrEmpty(s.Value.ParentStation))
            .Select(s => s.Value.ParentStation)
            .ToHashSet();
        var stops = feed.Stops
            .Where(s => usedStopIds.Contains(s.Value.StopId) || parentIds.Contains(s.Value.StopId))
            .ToDictionary();
        var allStopIds = stops.Select(s => s.Value.StopId).ToHashSet();

        // Calendriers (filtrés par service + dates)
        var calendars = feed.Calendars.ToDictionary();
        var calendarDates = feed.CalendarDates
            .Where(cd => serviceIds.Contains(cd.Value.ServiceId))
            .ToDictionary();


        return new GtfsFeed
        {
            Agencies      = agencies,
            Routes        = routes,
            Stops         = stops,
            Trips         = trips,
            StopTimes     = stopTimes,
            Calendars     = calendars,
            CalendarDates = calendarDates,
            FeedInfos     = feed.FeedInfos, 
            Frequencies   = [],
            Transfers     = [],
        };
    }

}
