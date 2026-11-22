using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;

// Shared SQLite database for all DB-backed tests. A temporary FILE (not :memory:)
// is required because DbContext pooling and multiple contexts would otherwise see
// different in-memory databases.
[CollectionDefinition("sqlite")]
public sealed class SqliteCollection : ICollectionFixture<SqliteFixture> { }

public sealed class SqliteFixture : IAsyncLifetime
{
    private readonly string _dbPath;
    public string ConnectionString { get; private set; } = "";
    public bool Available { get; private set; }

    public SqliteFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"openlethe-tests-{Guid.NewGuid():N}.db");
    }

    public Task InitializeAsync()
    {
        ConnectionString = $"Data Source={_dbPath}";
        Available = true;
        return using_ctx_migrate();
    }

    private async Task using_ctx_migrate()
    {
        await using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
    }

    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    // Kept for source compatibility with existing [SkippableFact] tests.
    public void RequireDb()
    {
        Skip.IfNot(Available, "SQLite test database unavailable.");
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { /* best effort */ }
        try { File.Delete(_dbPath + "-wal"); } catch { /* best effort */ }
        try { File.Delete(_dbPath + "-shm"); } catch { /* best effort */ }
        return Task.CompletedTask;
    }
}
