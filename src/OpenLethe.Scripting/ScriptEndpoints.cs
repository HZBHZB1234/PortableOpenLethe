using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;

namespace OpenLethe.Scripting;

/// Fixed endpoints for user scripts. No routes are registered dynamically:
/// scripts expose named actions invoked through /api/Script/Invoke.
public static class ScriptEndpoints
{
    private static readonly JsonSerializerOptions WireOptions = new() { IncludeFields = true };

    public sealed class InvokeRequest
    {
        public string scriptName { get; set; } = "";
        public string action { get; set; } = "";
        public JsonElement args { get; set; }
    }
    public sealed class InstallRequest
    {
        public string name { get; set; } = "";
        public string source { get; set; } = "";
    }
    public sealed class NameRequest { public string name { get; set; } = ""; }

    public static IEndpointRouteBuilder MapScripts(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/Script/list", async (HttpContext ctx, AppDbContext db) =>
        {
            var a = await AccountFor(ctx, db);
            if (a is null) return Results.Unauthorized();
            var rows = await db.UserScripts.AsNoTracking().Where(s => s.AccountId == a.Id)
                .Select(s => new { s.Id, s.Name, s.Enabled, s.Revision, s.UpdatedAt }).ToListAsync();
            return Results.Json(new { scripts = rows });
        });

        app.MapPost("/api/Script/install", async (HttpContext ctx, AppDbContext db, InstallRequest req) =>
        {
            var a = await AccountFor(ctx, db);
            if (a is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.name) || req.source.Length is < 1 or > 65536)
                return Results.BadRequest();
            if (await db.UserScripts.AnyAsync(s => s.AccountId == a.Id && s.Name == req.name))
                return Results.Conflict();
            var now = DateTime.UtcNow;
            db.UserScripts.Add(new UserScript
            {
                Id = Guid.NewGuid(), AccountId = a.Id, Name = req.NameOr(req.name),
                Source = req.source, Enabled = true, Revision = now.Ticks, CreatedAt = now, UpdatedAt = now,
            });
            await db.SaveChangesAsync();
            return Results.Json(new { ok = true });
        });

        app.MapPost("/api/Script/update", async (HttpContext ctx, AppDbContext db, InstallRequest req) =>
        {
            var a = await AccountFor(ctx, db);
            if (a is null) return Results.Unauthorized();
            var s = await db.UserScripts.SingleOrDefaultAsync(x => x.AccountId == a.Id && x.Name == req.name);
            if (s is null) return Results.NotFound();
            s.Source = req.source;
            s.Revision = DateTime.UtcNow.Ticks;
            s.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Json(new { ok = true });
        });

        app.MapPost("/api/Script/set-enabled", async (HttpContext ctx, AppDbContext db,
            NameRequest req, bool enabled) =>
        {
            var a = await AccountFor(ctx, db);
            if (a is null) return Results.Unauthorized();
            var s = await db.UserScripts.SingleOrDefaultAsync(x => x.AccountId == a.Id && x.Name == req.name);
            if (s is null) return Results.NotFound();
            s.Enabled = enabled;
            await db.SaveChangesAsync();
            return Results.Json(new { ok = true });
        });

        app.MapPost("/api/Script/remove", async (HttpContext ctx, AppDbContext db, NameRequest req) =>
        {
            var a = await AccountFor(ctx, db);
            if (a is null) return Results.Unauthorized();
            var s = await db.UserScripts.SingleOrDefaultAsync(x => x.AccountId == a.Id && x.Name == req.name);
            if (s is null) return Results.NotFound();
            db.UserScripts.Remove(s);
            var grants = db.ScriptGrants.Where(g => g.ScriptId == s.Id);
            db.ScriptGrants.RemoveRange(grants);
            await db.SaveChangesAsync();
            return Results.Json(new { ok = true });
        });

        app.MapPost("/api/Script/Invoke", async (HttpContext ctx, AppDbContext db,
            ILoggerFactory loggerFactory, InvokeRequest req) =>
        {
            var a = await AccountFor(ctx, db);
            if (a is null) return Results.Unauthorized();
            var runtime = ScriptRegistry.LoadForAccount(db, a.Id)
                .FirstOrDefault(r => r.Entity.Name == req.scriptName);
            if (runtime is null) return Results.NotFound();
            if (!runtime.Actions.ContainsKey(req.action)) return Results.BadRequest("unknown action");

            var logger = loggerFactory.CreateLogger("Script");
            try
            {
                var runner = new ScriptRunner(runtime, a, db, logger);
                runner.Execute();
                var arg = runner.ParseJsonValue(req.args.ValueKind == JsonValueKind.Undefined
                    ? "{}" : req.args.GetRawText());
                var result = runner.Invoke(runtime.Actions[req.action], arg);
                if (result is null || result == Jint.Native.JsValue.Undefined || result == Jint.Native.JsValue.Null)
                    return Results.Json(new { ok = true, result = (object?)null }, WireOptions);
                return Results.Json(new
                {
                    ok = true,
                    result = JsonSerializer.Deserialize<JsonElement>(runner.Stringify(result)),
                }, WireOptions);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Script {Name} action {Action} failed", req.scriptName, req.action);
                return Results.Json(new { ok = false, error = e.Message }, statusCode: 500);
            }
        });

        return app;
    }

    private static async Task<Account?> AccountFor(HttpContext ctx, AppDbContext db)
    {
        if (ctx.Items["sub"] is not string sub) return null;
        return await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Username == sub);
    }
}

internal static class InstallRequestExtensions
{
    // Guard against a null/whitespace name slipping through the constructor default.
    public static string NameOr(this ScriptEndpoints.InstallRequest req, string raw) => string.IsNullOrWhiteSpace(raw) ? "unnamed" : raw.Trim();
}





