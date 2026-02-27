namespace GTFSDemo.Api.Configuration;

public class OpenTransportDataOptions
{
    public const string SectionName = "OpenTransportData";

    /// <summary>Clé API opentransportdata.swiss (utilisée pour les flux GTFS-RT)</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>URL du ZIP GTFS statique (timetable TL)</summary>
    public string GtfsStaticUrl { get; set; } = string.Empty;

    /// <summary>URL flux GTFS-RT unique (vehicles + trip-updates dans un seul FeedMessage)</summary>
    public string GtfsRtUrl { get; set; } = string.Empty;

    /// <summary>Filtre sur agency_id (laisser vide = toutes les agences)</summary>
    public string AgencyId { get; set; } = string.Empty;
}
