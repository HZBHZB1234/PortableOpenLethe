using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OpenLethe.Server;

/// Applies the SQLite settings a portable local server needs on every pooled
/// connection: WAL (readers don't block the writer), a busy timeout (transient
/// write contention resolves instead of throwing SQLITE_BUSY), and foreign keys.
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string Pragma =
        "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Apply(connection);

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default) =>
        await ApplyAsync(connection, ct);

    private static void Apply(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragma;
        cmd.ExecuteNonQuery();
    }

    private static async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragma;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
