namespace GTFSDemo.Processor.Models;

public class GtfsFeed
{
    public Dictionary<string, Agency> Agencies { get; set; } = [];
    public Dictionary<string, Route> Routes { get; set; } = [];
    public Dictionary<string, Stop> Stops { get; set; } = [];
    public Dictionary<string, Trip> Trips { get; set; } = [];
    public Dictionary<string, StopTime> StopTimes { get; set; } = [];
    public Dictionary<string, Calendar> Calendars { get; set; } = [];
    public Dictionary<string, CalendarDate> CalendarDates { get; set; } = [];
    public Dictionary<string, FeedInfo> FeedInfos { get; set; } = [];
    public Dictionary<string, Frequency> Frequencies { get; set; } = [];
    public Dictionary<string, Transfer> Transfers { get; set; } = [];

    public void Build()
    {
        foreach (Stop stop in Stops.Values)
        {
            if (!stop.IsStation)
            {
                stop.Station = Stops[stop.ParentStation];
            }
        }
        foreach (Agency agency in Agencies.Values)
        {
            agency.Routes = new List<Route>();
        }
        foreach (Route route in Routes.Values)
        {
            route.Agency = Agencies[route.AgencyId];
            route.Agency.Routes.Add(route);
            route.Trips = new List<Trip>();
        }
        foreach (Trip trip in Trips.Values)
        {
            trip.Route = Routes[trip.RouteId];
            trip.Route.Trips.Add(trip);
            trip.StopTimes = new List<StopTime>();
        }
        foreach (StopTime stopTime in StopTimes.Values)
        {
            stopTime.Trip = Trips[stopTime.TripId];
            stopTime.Stop = Stops[stopTime.StopId];
            stopTime.Trip.StopTimes.Add(stopTime);
        } 
    }
}
