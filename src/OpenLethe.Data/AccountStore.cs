using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OpenLethe.Data;

public sealed class AccountStore(AppDbContext ctx)
{
    public Task<Account?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        ctx.Accounts.SingleOrDefaultAsync(a => a.Username == username, ct);

    /// Just the IngameId, for callers that only need to know the account exists and who
    /// it is. Loading the entity instead would detoast all fifteen jsonb documents.
    public Task<int?> FindIngameIdAsync(string username, CancellationToken ct = default) =>
        ctx.Accounts.Where(a => a.Username == username)
            .Select(a => (int?)a.IngameId).SingleOrDefaultAsync(ct);

    public Task<Account?> FindByDiscordIdAsync(string discordId, CancellationToken ct = default) =>
        ctx.Accounts.SingleOrDefaultAsync(a => a.DiscordId == discordId, ct);

    /// Rust login_authorized: get_by_discord_id, else create.
    ///
    /// The account is named by the SNOWFLAKE, not by the Discord display name.
    /// Username is this server's identity key - it is what every handler resolves
    /// the JWT `sub` against - so it has to be unique and stable, which a display
    /// name is neither. The display name travels in the token's `name` claim, which
    /// is where Rust keeps it too.
    ///
    /// Null when a non-Discord account already squats that username; the caller
    /// must not adopt it, or the squatter's token would resolve to this account.
    public async Task<Account?> GetOrCreateByDiscordIdAsync(
        string discordId, CancellationToken ct = default)
    {
        var existing = await FindByDiscordIdAsync(discordId, ct);
        if (existing is not null) return existing;

        if (await FindByUsernameAsync(discordId, ct) is not null) return null;

        // One save, not two: a row written without its DiscordId would be an
        // unclaimed account named after the snowflake, which /auth/login would then
        // hand to anyone asking for it.
        var created = await CreateAsync(discordId, ct, discordId);
        if (created is not null) return created;

        // Lost the insert race to a concurrent first login; their row is ours too.
        return await FindByDiscordIdAsync(discordId, ct);
    }

    /// Null when the username belongs to a Discord-linked account. Those are
    /// reachable only through the OAuth flow: /auth/login takes any username on
    /// trust, so without this guard anyone could name a Discord user's account and
    /// be handed a token that resolves to it.
    public async Task<Account?> GetOrCreateByUsernameAsync(string username, CancellationToken ct = default)
    {
        var existing = await FindByUsernameAsync(username, ct);
        if (existing is not null) return existing.DiscordId is null ? existing : null;

        var created = await CreateAsync(username, ct);
        if (created is not null) return created;

        // Lost the insert race to a concurrent first login; re-apply the same rule.
        existing = await FindByUsernameAsync(username, ct);
        return existing?.DiscordId is null ? existing : null;
    }

    /// Null on a unique-key collision: a concurrent request created the row between
    /// our find and our insert. Callers re-fetch; the unique indexes guarantee the
    /// winner's row is the one they get. IngameId is allocated from a counter row
    /// inside the same transaction, so ids never collide no matter how many writers race.
    private async Task<Account?> CreateAsync(string username, CancellationToken ct, string? discordId = null)
    {
        var now = DateTime.UtcNow;
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            DiscordId = discordId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        try
        {
            var counter = await ctx.Counters.SingleOrDefaultAsync(c => c.Key == "IngameId", ct);
            if (counter is null)
            {
                counter = new Counter { Key = "IngameId", Value = 1 };
                ctx.Counters.Add(counter);
            }
            else counter.Value++;

            account.IngameId = (int)counter.Value;
            ctx.Accounts.Add(account);
            await ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return account;
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            await tx.RollbackAsync(ct);
            // Detach failed entities so this context stays usable for the re-fetch.
            ctx.Entry(account).State = EntityState.Detached;
            var c = ctx.Counters.Local.FirstOrDefault(x => x.Key == "IngameId");
            if (c is not null) ctx.Entry(c).State = EntityState.Detached;
            return null;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException e) =>
        e.InnerException is SqliteException { SqliteErrorCode: 19 };
}
