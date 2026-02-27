using GTFSDemo.Api.Configuration;
using GTFSDemo.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<OpenTransportDataOptions>(
    builder.Configuration.GetSection(OpenTransportDataOptions.SectionName));

// ── HTTP ─────────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient();

// Client dédié GTFS-RT : décompression gzip automatique + suivi des redirections
builder.Services.AddHttpClient("gtfsrt")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip
                               | System.Net.DecompressionMethods.Deflate,
        AllowAutoRedirect = true,
    });

// ── Caching ──────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CacheService>();

// ── Services métier ───────────────────────────────────────────────────────────
// GtfsStaticService : singleton + IHostedService (chargement au démarrage)
builder.Services.AddSingleton<GtfsStaticService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GtfsStaticService>());

// GtfsRealtimeService : singleton + BackgroundService (refresh toutes les 30 s)
builder.Services.AddSingleton<GtfsRealtimeService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GtfsRealtimeService>());

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ── CORS (React dev server sur :5173) ────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── Swagger (dev) ─────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Run();
