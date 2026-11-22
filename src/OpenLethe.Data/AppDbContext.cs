using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OpenLethe.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<UserScript> UserScripts => Set<UserScript>();
    public DbSet<ScriptGrant> ScriptGrants => Set<ScriptGrant>();

    /// Every game-data column; each is stored as TEXT holding a JSON document.
    internal static readonly string[] JsonColumns =
    {
        nameof(Account.Formations), nameof(Account.Egos), nameof(Account.Personalities),
        nameof(Account.Announcers), nameof(Account.UserInfo), nameof(Account.CustomTheme),
        nameof(Account.CustomIdentities), nameof(Account.CustomEgos),
        nameof(Account.CustomEgoGifts), nameof(Account.CustomEgoGiftDropPools),
        nameof(Account.CustomMdBattles), nameof(Account.CustomMdCommonData),
        nameof(Account.MdSaveInfo), nameof(Account.StorySaveInfo), nameof(Account.StoryMdSaveInfo),
        nameof(Account.RailwaySaveInfo),
        nameof(Account.ChapterState), nameof(Account.BossRaidSaveInfo),
        nameof(Account.Chapter10RpgSaveInfo),
    };

    /// The uploads an MdCatalog is built from; a change to any of them restamps CustomMdRevision.
    public static readonly string[] CustomMdColumns =
    {
        nameof(Account.CustomTheme), nameof(Account.CustomEgoGifts), nameof(Account.CustomEgoGiftDropPools),
        nameof(Account.CustomMdBattles), nameof(Account.CustomMdCommonData),
    };

    protected override void OnModelCreating(ModelBuilder b)
    {
        var a = b.Entity<Account>();
        a.ToTable("accounts");
        a.HasKey(x => x.Id);
        a.HasIndex(x => x.Username).IsUnique();
        a.HasIndex(x => x.IngameId).IsUnique();
        // IngameId is allocated from the counters table inside a transaction
        // (SQLite AUTOINCREMENT only works on INTEGER PRIMARY KEY, and Id is the PK).
        // Filtered: dev-login accounts share DiscordId == null.
        a.HasIndex(x => x.DiscordId).IsUnique().HasFilter("\"DiscordId\" IS NOT NULL");
        a.Property(x => x.Username).IsRequired();

        b.Entity<Counter>().ToTable("counters").HasKey(x => x.Key);

        b.Entity<UserScript>().ToTable("user_scripts").HasKey(x => x.Id);
        b.Entity<UserScript>().HasIndex(x => new { x.AccountId, x.Name }).IsUnique();
        b.Entity<ScriptGrant>().ToTable("script_grants").HasKey(x => x.Id);
        b.Entity<ScriptGrant>().HasIndex(x => new { x.ScriptId, x.GranteeAccountId }).IsUnique();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        DropUnchangedDocuments();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        DropUnchangedDocuments();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    /// A JSON document read back and re-serialized may differ in key order/whitespace from
    /// what was loaded, so EF would rewrite unchanged large documents. Compare semantically
    /// and unmark unchanged ones to avoid needless writes (and WAL growth on SQLite).
    ///
    /// A custom MD upload that really changed restamps CustomMdRevision. The stamp is a fresh
    /// value, not an increment: a scoped upload never loaded the old one. The client re-sends
    /// every upload at each login, and an unchanged one keeps its stamp and its cached catalog.
    private void DropUnchangedDocuments()
    {
        foreach (var e in ChangeTracker.Entries<Account>())   // runs DetectChanges
        {
            if (e.State != EntityState.Modified) continue;
            foreach (var name in JsonColumns)
            {
                var p = e.Property(name);
                if (p.IsModified && SameJson(p.OriginalValue as string, p.CurrentValue as string))
                    p.IsModified = false;
            }

            if (CustomMdColumns.Any(name => e.Property(name).IsModified))
                e.Entity.CustomMdRevision = Math.Max(DateTime.UtcNow.Ticks, e.Entity.CustomMdRevision + 1);
        }
        ChangeTracker.DetectChanges();
    }

    // A null original is a scoped read's unloaded column (see HandlerContext): never equal,
    // so a write to it still reaches SaveAsync's guard and the database.
    private static bool SameJson(string? a, string? b)
    {
        if (a is null || b is null) return false;
        if (a == b) return true;
        try
        {
            using var x = JsonDocument.Parse(a);
            using var y = JsonDocument.Parse(b);
            return JsonElement.DeepEquals(x.RootElement, y.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
