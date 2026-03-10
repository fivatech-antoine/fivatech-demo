using GTFSDemo.Processor;

const string DefaultSource = @"D:\projets\DemoGTFS\GTFS Statique\Source";
const string DefaultResult = @"D:\projets\DemoGTFS\GTFS Statique\Result";

var sourceDir = args.Length > 0 ? args[0] : DefaultSource;
var resultDir = args.Length > 1 ? args[1] : DefaultResult;

Console.WriteLine($"Source  : {sourceDir}");
Console.WriteLine($"Résultat: {resultDir}");
Console.WriteLine();

// ── Lecture ───────────────────────────────────────────────────────────────────
Console.WriteLine("=== Lecture du flux GTFS ===");
var feed = new GtfsReader(sourceDir).Read();
Console.WriteLine();

// Construction
Console.WriteLine("=== Consolidation des données GTFS ===");
feed.Build();
Console.WriteLine();


// ── Filtrage ──────────────────────────────────────────────────────────────────
Console.WriteLine("=== Application des filtres ===");
var filter = new GtfsFilter();

var filtered = filter.Apply(feed);

Console.WriteLine($"  Agences       : {filtered.Agencies.Count,8} / {feed.Agencies.Count}");
Console.WriteLine($"  Lignes        : {filtered.Routes.Count,8} / {feed.Routes.Count}");
Console.WriteLine($"  Arrêts        : {filtered.Stops.Count,8} / {feed.Stops.Count}");
Console.WriteLine($"  Trajets       : {filtered.Trips.Count,8} / {feed.Trips.Count}");
Console.WriteLine($"  Horaires      : {filtered.StopTimes.Count,8} / {feed.StopTimes.Count}");
Console.WriteLine($"  Calendriers   : {filtered.Calendars.Count,8} / {feed.Calendars.Count}");
Console.WriteLine($"  Exceptions    : {filtered.CalendarDates.Count,8} / {feed.CalendarDates.Count}");
Console.WriteLine($"  Fréquences    : {filtered.Frequencies.Count,8} / {feed.Frequencies.Count}");
Console.WriteLine($"  Transferts    : {filtered.Transfers.Count,8} / {feed.Transfers.Count}");
Console.WriteLine();

// ── Écriture ──────────────────────────────────────────────────────────────────
Console.WriteLine("=== Écriture du flux filtré ===");
new GtfsWriter(resultDir).Write(filtered);
Console.WriteLine();

Console.WriteLine("=== Terminé ===");
