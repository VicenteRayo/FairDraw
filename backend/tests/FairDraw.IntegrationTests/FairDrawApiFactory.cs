using FairDraw.Api.Testing;
using FairDraw.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairDraw.IntegrationTests;

/// <summary>
/// Boots the real FairDraw.Api pipeline (Program.cs, all endpoints, auth filters, services) but,
/// via TestHooks + the "Testing" environment, points it at a SQLite database instead of Postgres.
///
/// Uses a uniquely-named SQLite shared-cache in-memory database (connection STRING, not a single
/// shared open connection instance) so that each DbContext the app creates opens its own physical
/// connection — necessary for the commit/reveal concurrency tests to exercise genuinely concurrent
/// transactions rather than serializing on one shared ADO.NET connection object. A "keep-alive"
/// connection is held open for the factory's lifetime so the in-memory database isn't dropped
/// between requests (SQLite destroys a shared in-memory database once its last connection closes).
/// The schema is built via EnsureCreated() straight from the current EF model rather than by
/// replaying the Npgsql-specific migration SQL, which is not guaranteed to be portable across
/// providers.
/// </summary>
public class FairDrawApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;

    public FairDrawApiFactory()
    {
        var dbName = $"fairdraw_test_{Guid.NewGuid():N}";
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbName,
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();

        TestHooks.SqliteConnectionString = _connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FairDrawDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
            TestHooks.SqliteConnectionString = null;
        }
    }
}
