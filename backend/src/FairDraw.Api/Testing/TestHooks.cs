namespace FairDraw.Api.Testing;

/// <summary>
/// A narrow, explicit seam for the integration test host only. Set by FairDrawApiFactory before
/// building the host, and only ever consulted by Program.cs when running under the "Testing"
/// environment. Kept deliberately tiny (a single nullable field) rather than trying to remove and
/// re-register EF Core's Npgsql services from the DI container after the fact, which trips EF's
/// "multiple database providers registered" guard because of how its internal service provider
/// caching keys on the DbContext type.
///
/// A connection STRING (SQLite shared-cache in-memory), not a single shared open DbConnection
/// instance, so that each DbContext created via the factory opens its OWN physical connection —
/// required for genuinely concurrent transactions (commit/reveal race tests). A single shared
/// SqliteConnection object can only ever have one active transaction at a time, which made the
/// concurrency tests fail outright rather than exercise the retry logic they're meant to test.
/// </summary>
public static class TestHooks
{
    public static string? SqliteConnectionString { get; set; }
}
