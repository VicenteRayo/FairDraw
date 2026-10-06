using FairDraw.Api.Endpoints;
using FairDraw.Api.Errors;
using FairDraw.Api.Services;
using FairDraw.Api.Testing;
using FairDraw.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The integration test host (FairDrawApiFactory) runs under the "Testing" environment and supplies
// its own open SQLite connection via TestHooks instead of a real Postgres one. Branching here,
// rather than registering Npgsql and later trying to remove/replace it in ConfigureTestServices,
// avoids EF Core's "multiple database providers registered" guard entirely.
var isTestingEnvironment = builder.Environment.IsEnvironment("Testing");

if (isTestingEnvironment)
{
    var sqliteConnectionString = TestHooks.SqliteConnectionString
        ?? throw new InvalidOperationException("TestHooks.SqliteConnectionString must be set before building the host under the Testing environment.");
    builder.Services.AddDbContextFactory<FairDrawDbContext>(options => options.UseSqlite(sqliteConnectionString));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("FairDraw")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:FairDraw configuration.");
    builder.Services.AddDbContextFactory<FairDrawDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DrawService>();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<CommitmentService>();
builder.Services.AddScoped<RevealService>();
builder.Services.AddScoped<VerificationService>();
// Singleton, not scoped: it is consumed by the (singleton) DrawExpirationHostedService, and it is
// stateless — it only depends on singletons and creates its own DbContext per sweep.
builder.Services.AddSingleton<DrawExpirationPurger>();

// Not registered under Testing: the purge logic is unit-tested directly against
// DrawExpirationPurger, and a live timer loop would just add nondeterminism to the test suite.
if (!isTestingEnvironment)
{
    builder.Services.AddHostedService<DrawExpirationHostedService>();
}

// Postgres-specific healthcheck only makes sense (and only compiles a valid connection) outside
// the Testing environment; the SQLite test host has nothing meaningful to health-check here.
if (!isTestingEnvironment)
{
    var healthCheckConnectionString = builder.Configuration.GetConnectionString("FairDraw")!;
    builder.Services.AddHealthChecks().AddNpgSql(healthCheckConnectionString, name: "postgres");
}
else
{
    builder.Services.AddHealthChecks();
}

// CORS only for explicitly configured origins (Cors:AllowedOrigins, e.g. the Vite dev server in
// docker-compose.yml). In production the frontend and API share the same origin
// (vicenterayo.com/proyectos/fairdraw/), so no origin is configured and no CORS headers are sent.
const string CorsPolicy = "ConfiguredOrigins";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (corsOrigins.Length > 0)
{
    app.UseCors(CorsPolicy);
}

// Global error handling: known DomainExceptions become clean `{ "error": code }` JSON with the
// right status code; anything else becomes a generic 500 with no internal detail. Neither path
// ever logs request bodies (which could contain a participant's number/secret) — only the
// exception type/message, which by construction never carries that payload (see §17 of the spec).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        if (feature?.Error is DomainException domainEx)
        {
            context.Response.StatusCode = domainEx.StatusCode;
            await context.Response.WriteAsJsonAsync(new { error = domainEx.Code });
            return;
        }

        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");
        logger.LogError(feature?.Error, "Unhandled exception while processing {Path}", context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        if (isTestingEnvironment)
        {
            await context.Response.WriteAsJsonAsync(new { error = "internal_error", debug = feature?.Error?.ToString() });
            return;
        }
        await context.Response.WriteAsJsonAsync(new { error = "internal_error" });
    });
});

app.MapHealthChecks("/health");
app.MapDrawsEndpoints();

// Skipped under the integration test host, which builds its schema directly from the current
// model via EnsureCreated() against SQLite instead of replaying Npgsql-specific migration SQL.
if (!app.Environment.IsEnvironment("Testing"))
{
    await MigrateDatabaseAsync(app);
}

app.Run();

static async Task MigrateDatabaseAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<FairDrawDbContext>>();

    const int maxAttempts = 5;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migration applied successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Database not ready yet (attempt {Attempt}/{MaxAttempts}); retrying in 3s...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Exposed so WebApplicationFactory<Program> can be used from integration tests.
public partial class Program;
