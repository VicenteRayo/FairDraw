using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FairDraw.Api.Services;

/// <summary>
/// Detects transient conflicts from concurrent Serializable transactions so the caller can retry.
/// Under Postgres (Npgsql) this is a real serialization_failure/deadlock_detected error code.
/// Under SQLite (used in tests) there is no such thing — instead, a Serializable transaction maps
/// to an immediate write lock, so a second concurrent writer gets SQLITE_BUSY/SQLITE_LOCKED
/// (potentially even while just opening the transaction, not only on SaveChanges) rather than a
/// clean "your snapshot was invalidated" signal. Retrying on both keeps the same code path correct
/// in both environments instead of needing SQLite-only special-casing at the call sites.
/// </summary>
public static class TransactionHelpers
{
    public const int DefaultMaxAttempts = 5;

    public static bool IsTransientConflict(Exception ex) => ex switch
    {
        DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } } => true,
        DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 5 or 6 } } => true,
        SqliteException { SqliteErrorCode: 5 or 6 } => true, // SQLITE_BUSY / SQLITE_LOCKED
        _ => false
    };

    /// <summary>
    /// True if the failure was a unique-index violation (e.g. the rare true-simultaneous double
    /// commit/reveal insert that slips past the application-level existence check). The caller
    /// should translate this into a clean 409 Conflict rather than a raw 500.
    /// </summary>
    public static bool IsUniqueConstraintViolation(DbUpdateException ex) => ex.InnerException switch
    {
        PostgresException { SqlState: "23505" } => true,
        SqliteException { SqliteErrorCode: 19 } => true, // SQLITE_CONSTRAINT
        _ => false
    };
}
