using CsvHelper.Configuration;
using GTFSDemo.Processor.Models;

namespace GTFSDemo.Processor.Csv;

public sealed class AgencyMap : ClassMap<Agency>
{
    public AgencyMap()
    {
        Map(m => m.AgencyId).Name("agency_id");
        Map(m => m.Name).Name("agency_name");
        Map(m => m.Url).Name("agency_url");
        Map(m => m.Timezone).Name("agency_timezone");
        Map(m => m.Lang).Name("agency_lang");
        Map(m => m.Phone).Name("agency_phone");
    }
}

public sealed class RouteMap : ClassMap<Route>
{
    public RouteMap()
    {
        Map(m => m.RouteId).Name("route_id");
        Map(m => m.AgencyId).Name("agency_id");
        Map(m => m.ShortName).Name("route_short_name");
        Map(m => m.LongName).Name("route_long_name");
        Map(m => m.Desc).Name("route_desc");
        Map(m => m.RouteType).Name("route_type");
    }
}

public sealed class StopMap : ClassMap<Stop>
{
    public StopMap()
    {
        Map(m => m.StopId).Name("stop_id");
        Map(m => m.Name).Name("stop_name");
        Map(m => m.Lat).Name("stop_lat");
        Map(m => m.Lon).Name("stop_lon");
        Map(m => m.LocationType).Name("location_type");
        Map(m => m.ParentStation).Name("parent_station");
        Map(m => m.PlatformCode).Name("platform_code");
        Map(m => m.OriginalStopId).Name("original_stop_id");
    }
}

public sealed class TripMap : ClassMap<Trip>
{
    public TripMap()
    {
        Map(m => m.RouteId).Name("route_id");
        Map(m => m.ServiceId).Name("service_id");
        Map(m => m.TripId).Name("trip_id");
        Map(m => m.Headsign).Name("trip_headsign");
        Map(m => m.ShortName).Name("trip_short_name");
        Map(m => m.DirectionId).Name("direction_id");
        Map(m => m.BlockId).Name("block_id");
        Map(m => m.OriginalTripId).Name("original_trip_id");
        Map(m => m.Hints).Name("hints");
    }
}

public sealed class StopTimeMap : ClassMap<StopTime>
{
    public StopTimeMap()
    {
        Map(m => m.TripId).Name("trip_id");
        Map(m => m.ArrivalTime).Name("arrival_time");
        Map(m => m.DepartureTime).Name("departure_time");
        Map(m => m.StopId).Name("stop_id");
        Map(m => m.StopSequence).Name("stop_sequence");
        Map(m => m.PickupType).Name("pickup_type");
        Map(m => m.DropOffType).Name("drop_off_type");
    }
}

public sealed class CalendarMap : ClassMap<Calendar>
{
    public CalendarMap()
    {
        Map(m => m.ServiceId).Name("service_id");
        Map(m => m.Monday).Name("monday");
        Map(m => m.Tuesday).Name("tuesday");
        Map(m => m.Wednesday).Name("wednesday");
        Map(m => m.Thursday).Name("thursday");
        Map(m => m.Friday).Name("friday");
        Map(m => m.Saturday).Name("saturday");
        Map(m => m.Sunday).Name("sunday");
        Map(m => m.StartDate).Name("start_date");
        Map(m => m.EndDate).Name("end_date");
    }
}

public sealed class CalendarDateMap : ClassMap<CalendarDate>
{
    public CalendarDateMap()
    {
        Map(m => m.ServiceId).Name("service_id");
        Map(m => m.Date).Name("date");
        Map(m => m.ExceptionType).Name("exception_type");
    }
}

public sealed class FeedInfoMap : ClassMap<FeedInfo>
{
    public FeedInfoMap()
    {
        Map(m => m.PublisherName).Name("feed_publisher_name");
        Map(m => m.PublisherUrl).Name("feed_publisher_url");
        Map(m => m.Lang).Name("feed_lang");
        Map(m => m.StartDate).Name("feed_start_date");
        Map(m => m.EndDate).Name("feed_end_date");
        Map(m => m.Version).Name("feed_version");
    }
}

public sealed class FrequencyMap : ClassMap<Frequency>
{
    public FrequencyMap()
    {
        Map(m => m.TripId).Name("trip_id");
        Map(m => m.StartTime).Name("start_time");
        Map(m => m.EndTime).Name("end_time");
        Map(m => m.HeadwaySecs).Name("headway_secs");
        Map(m => m.ExactTimes).Name("exact_times");
    }
}

public sealed class TransferMap : ClassMap<Transfer>
{
    public TransferMap()
    {
        Map(m => m.FromStopId).Name("from_stop_id");
        Map(m => m.ToStopId).Name("to_stop_id");
        Map(m => m.FromRouteId).Name("from_route_id");
        Map(m => m.ToRouteId).Name("to_route_id");
        Map(m => m.FromTripId).Name("from_trip_id");
        Map(m => m.ToTripId).Name("to_trip_id");
        Map(m => m.TransferType).Name("transfer_type");
        Map(m => m.MinTransferTime).Name("min_transfer_time");
    }
}
