using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;
using OpenLethe.Server.Auth;

namespace OpenLethe.Server.Handlers;

/// Local management API. Tokens are carried in the JSON body (like /dashboard),
/// and every route except /admin/login requires an `admin` claim.
public static class AdminEndpoints
{
    private static readonly string[] DocumentFields =
    {
        nameof(Account.Formations), nameof(Account.Egos), nameof(Account.Personalities),
        nameof(Account.Announcers), nameof(Account.UserInfo), nameof(Account.CustomTheme),
        nameof(Account.CustomIdentities), nameof(Account.CustomEgos), nameof(Account.CustomEgoGifts),
        nameof(Account.CustomEgoGiftDropPools), nameof(Account.CustomMdBattles),
        nameof(Account.CustomMdCommonData), nameof(Account.MdSaveInfo), nameof(Account.StorySaveInfo),
        nameof(Account.StoryMdSaveInfo), nameof(Account.RailwaySaveInfo), nameof(Account.ChapterState),
        nameof(Account.Chapter10RpgSaveInfo), nameof(Account.BossRaidSaveInfo),
    };

    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/login", async (HttpContext ctx, IConfiguration cfg, JwtService jwt) =>
        {
            var body = await ReadBody(ctx);
            if (body is null || !body.Value.TryGetProperty("password", out var p)
                || p.ValueKind != JsonValueKind.String) return Results.Unauthorized();

            var configured = cfg["Admin:Password"];
            if (string.IsNullOrEmpty(configured))
            {
                var path = Path.Combine(AppContext.BaseDirectory, "data", "admin.secret");
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    configured = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
                    File.WriteAllText(path, configured);
                    Console.WriteLine($"[admin] generated password: {configured} (data/admin.secret)");
                }
                else configured = File.ReadAllText(path).Trim();
            }
            if (p.GetString() != configured) return Results.Unauthorized();
            return Results.Json(new { token = jwt.MintAdmin("admin") });
        });

        app.MapPost("/admin/accounts", Admin(async (db, _, _) =>
        {
            var rows = await db.Accounts.AsNoTracking()
                .OrderBy(a => a.IngameId)
                .Select(a => new { a.Id, a.Username, a.IngameId, a.DiscordId, a.CreatedAt, a.UpdatedAt })
                .ToListAsync();
            return Results.Json(new { accounts = rows });
        }));

        app.MapPost("/admin/accounts/create", Admin(async (db, body, store) =>
        {
            if (!body.TryGetProperty("username", out var u) || u.ValueKind != JsonValueKind.String)
                return Results.BadRequest();
            var name = u.GetString()!.Trim();
            if (name.Length == 0) return Results.BadRequest();
            var acc = await store.GetOrCreateByUsernameAsync(name);
            return acc is null ? Results.Conflict() : Results.Json(new { username = acc.Username, ingameId = acc.IngameId });
        }));

        app.MapPost("/admin/accounts/delete", Admin(async (db, body, _) =>
        {
            if (!body.TryGetProperty("username", out var u) || u.ValueKind != JsonValueKind.String)
                return Results.BadRequest();
            var acc = await db.Accounts.SingleOrDefaultAsync(a => a.Username == u.GetString());
            if (acc is null) return Results.NotFound();
            db.Accounts.Remove(acc);
            await db.SaveChangesAsync();
            return Results.Json(new { deleted = true });
        }));

        app.MapPost("/admin/accounts/ingameid", Admin(async (db, body, _) =>
        {
            if (!body.TryGetProperty("username", out var u) || u.ValueKind != JsonValueKind.String
                || !body.TryGetProperty("ingameId", out var id) || !id.TryGetInt32(out var value))
                return Results.BadRequest();
            var acc = await db.Accounts.SingleOrDefaultAsync(a => a.Username == u.GetString());
            if (acc is null) return Results.NotFound();
            if (value != acc.IngameId && await db.Accounts.AnyAsync(a => a.IngameId == value && a.Id != acc.Id))
                return Results.Conflict(new { error = "ingameId already taken" });
            acc.IngameId = value;
            acc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Json(new { ingameId = value });
        }));

        app.MapPost("/admin/accounts/document", Admin(async (db, body, _) =>
        {
            if (!body.TryGetProperty("username", out var u) || u.ValueKind != JsonValueKind.String
                || !body.TryGetProperty("field", out var f) || f.ValueKind != JsonValueKind.String
                || !body.TryGetProperty("value", out var v))
                return Results.BadRequest();
            var field = f.GetString()!;
            if (!DocumentFields.Contains(field)) return Results.BadRequest("unknown field");
            var acc = await db.Accounts.SingleOrDefaultAsync(a => a.Username == u.GetString());
            if (acc is null) return Results.NotFound();
            var json = v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText();
            typeof(Account).GetProperty(field)!.SetValue(acc, json);
            acc.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Json(new { ok = true });
        }));

        app.MapPost("/admin/config/schema", Admin(async (AppDbContext _, JsonElement __, AccountStore ___) =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "config-schema.json");
            if (!File.Exists(path)) path = Path.Combine(Directory.GetCurrentDirectory(), "config-schema.json");
            if (!File.Exists(path)) return Results.Json(new { keys = Array.Empty<string>() });
            return Results.Content(await File.ReadAllTextAsync(path), "application/json");
        }));

        app.MapPost("/admin/restart-request", Admin((AppDbContext _, JsonElement __, AccountStore ___) =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", "restart.requested");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
            return Task.FromResult(Results.Json(new { ok = true }));
        }));

        app.MapPost("/admin/scripts/grants", Admin(async (db, body, _) =>
        {
            if (!body.TryGetProperty("scriptId", out var sid) || !Guid.TryParse(sid.GetString(), out var scriptId)
                || !body.TryGetProperty("username", out var u) || u.ValueKind != JsonValueKind.String
                || !body.TryGetProperty("capabilities", out var caps) || caps.ValueKind != JsonValueKind.Array)
                return Results.BadRequest();

            var account = await db.Accounts.SingleOrDefaultAsync(a => a.Username == u.GetString());
            var script = await db.UserScripts.SingleOrDefaultAsync(s => s.Id == scriptId);
            if (account is null || script is null) return Results.NotFound();

            var list = caps.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String)
                .Select(c => c.GetString()!).ToList();
            var grant = await db.ScriptGrants.SingleOrDefaultAsync(g =>
                g.ScriptId == scriptId && g.GranteeAccountId == account.Id);
            if (grant is null)
            {
                grant = new OpenLethe.Data.ScriptGrant
                {
                    Id = Guid.NewGuid(), ScriptId = scriptId,
                    GranteeAccountId = account.Id, GrantedAt = DateTime.UtcNow,
                };
                db.ScriptGrants.Add(grant);
            }
            grant.Capabilities = System.Text.Json.JsonSerializer.Serialize(list);
            grant.GrantedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Json(new { ok = true, capabilities = list });
        }));

        return app;
    }

    private static Delegate Admin(Func<AppDbContext, JsonElement, AccountStore, Task<IResult>> handler) =>
        async (HttpContext ctx) =>
        {
            var body = await ReadBody(ctx);
            if (body is null) return Results.BadRequest();
            if (!body.Value.TryGetProperty("token", out var t) || t.ValueKind != JsonValueKind.String)
                return Results.Unauthorized();
            var jwt = ctx.RequestServices.GetRequiredService<JwtService>();
            if (!jwt.TryVerifyClaims(t.GetString()!, out var claims) || !claims.Admin)
                return Results.Unauthorized();
            var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
            var store = ctx.RequestServices.GetRequiredService<AccountStore>();
            return await handler(db, body.Value, store);
        };

    private static async Task<JsonElement?> ReadBody(HttpContext ctx)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            return doc.RootElement.Clone();
        }
        catch (JsonException) { return null; }
    }
}
