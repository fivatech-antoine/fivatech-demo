using System.Threading.RateLimiting;
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
        policy.WithOrigins("http://localhost:5173", "https://demo.fivatech.ch")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            })));

// ── Swagger (dev) ─────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});

app.UseCors();
app.UseRateLimiter();
app.MapControllers();

app.Run();
