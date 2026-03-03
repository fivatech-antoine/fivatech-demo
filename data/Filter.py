import os
import pandas as pd

# Bounding box Suisse Romande
LON_MIN, LON_MAX = 6.0, 7.6
LAT_MIN, LAT_MAX = 45.8, 47.5

SOURCE = "D:\projets\DemoGTFS\GTFS Statique\Source"
RESULT = "D:\projets\DemoGTFS\GTFS Statique\Result"

os.makedirs(RESULT, exist_ok=True)

print("Chargement stops...")
stops = pd.read_csv(f"{SOURCE}/stops.txt")
stops_in_bbox = stops[
    (stops.stop_lon >= LON_MIN) & (stops.stop_lon <= LON_MAX) &
    (stops.stop_lat >= LAT_MIN) & (stops.stop_lat <= LAT_MAX)
]
# Récupère les parent_station référencés par ces stops
parent_ids = set(stops_in_bbox.parent_station.dropna())

# Stops parents correspondants (peuvent être hors bounding box)
stops_parents = stops[stops.stop_id.isin(parent_ids)]

# Union des deux
stops_filtered = pd.concat([stops_in_bbox, stops_parents]).drop_duplicates(subset="stop_id")

stop_ids = set(stops_filtered.stop_id)
print(f"  {len(stops_filtered)} stops conservés sur {len(stops)}")

print("Chargement stop_times (fichier lourd, patience...)...")
stop_times = pd.read_csv(f"{SOURCE}/stop_times.txt")
stop_times_filtered = stop_times[stop_times.stop_id.isin(stop_ids)]
trip_ids = set(stop_times_filtered.trip_id)
print(f"  {len(stop_times_filtered)} stop_times conservés sur {len(stop_times)}")

print("Chargement trips...")
trips = pd.read_csv(f"{SOURCE}/trips.txt")
trips_filtered = trips[trips.trip_id.isin(trip_ids)]
route_ids   = set(trips_filtered.route_id)
service_ids = set(trips_filtered.service_id)
print(f"  {len(trips_filtered)} trips conservés sur {len(trips)}")

print("Chargement routes...")
routes = pd.read_csv(f"{SOURCE}/routes.txt")
routes_filtered = routes[routes.route_id.isin(route_ids)]
agency_ids = set(routes_filtered.agency_id)
print(f"  {len(routes_filtered)} routes conservées sur {len(routes)}")

print("Chargement agency...")
agencies = pd.read_csv(f"{SOURCE}/agency.txt")
agencies_filtered = agencies[agencies.agency_id.isin(agency_ids)]
print(f"  {len(agencies_filtered)} agences conservées sur {len(agencies)}")

print("Chargement calendar_dates...")
calendar_dates = pd.read_csv(f"{SOURCE}/calendar_dates.txt")
calendar_dates_filtered = calendar_dates[calendar_dates.service_id.isin(service_ids)]
print(f"  {len(calendar_dates_filtered)} calendar_dates conservés sur {len(calendar_dates)}")

# Calendar.txt (optionnel selon les feeds)
if os.path.exists(f"{SOURCE}/calendar.txt"):
    print("Chargement calendar...")
    calendar = pd.read_csv(f"{SOURCE}/calendar.txt")
    calendar_filtered = calendar[calendar.service_id.isin(service_ids)]
    calendar_filtered.to_csv(f"{RESULT}/calendar.txt", index=False)
    print(f"  {len(calendar_filtered)} calendar conservés sur {len(calendar)}")

print("Écriture des fichiers résultat...")
stops_filtered.to_csv(f"{RESULT}/stops.txt", index=False)
stop_times_filtered.to_csv(f"{RESULT}/stop_times.txt", index=False)
trips_filtered.to_csv(f"{RESULT}/trips.txt", index=False)
routes_filtered.to_csv(f"{RESULT}/routes.txt", index=False)
agencies_filtered.to_csv(f"{RESULT}/agency.txt", index=False)
calendar_dates_filtered.to_csv(f"{RESULT}/calendar_dates.txt", index=False)

# Copie les fichiers non filtrés s'ils existent
for f in ["feed_info.txt", "transfers.txt", "fare_attributes.txt", "fare_rules.txt"]:
    if os.path.exists(f"{SOURCE}/{f}"):
        pd.read_csv(f"{SOURCE}/{f}").to_csv(f"{RESULT}/{f}", index=False)

print("\nTerminé !")
print(f"Stops      : {len(stops_filtered)}")
print(f"Stop times : {len(stop_times_filtered)}")
print(f"Trips      : {len(trips_filtered)}")
print(f"Routes     : {len(routes_filtered)}")
print(f"Agences    : {len(agencies_filtered)}")
