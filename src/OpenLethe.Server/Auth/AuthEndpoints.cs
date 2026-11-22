using Microsoft.Extensions.Caching.Memory;
using OpenLethe.Data;

namespace OpenLethe.Server.Auth;

/// Portable build: local login replacing Discord OAuth. The launcher handoff
/// shape is preserved: GET /auth/login?session_id=... opens a local page, the
/// browser submits the username, and the launcher polls /auth/token/poll.
public static class AuthEndpoints
{
    public sealed class LoginRequest { public string username { get; set; } = ""; }
    public sealed class LoginResponse { public string token { get; set; } = ""; }
    public sealed class SessionRequest { public string session_id { get; set; } = ""; }
    public sealed class SubmitRequest { public string session_id { get; set; } = ""; public string token { get; set; } = ""; }

    private const string HandoffPrefix = "auth-handoff:";
    private static readonly TimeSpan HandoffTtl = TimeSpan.FromSeconds(60);

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        // JSON login for tools/tests: username-only, trust on first use.
        app.MapPost("/auth/login", async (LoginRequest req, AccountStore store, JwtService jwt,
            IConfiguration cfg, CancellationToken ct) =>
        {
            if (!cfg.GetValue("Auth:EnableLocalLogin", true)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(req.username)) return Results.BadRequest();
            var account = await store.GetOrCreateByUsernameAsync(req.username.Trim(), ct);
            if (account is null) return Results.Conflict();
            return Results.Json(new LoginResponse { token = jwt.MintLocal(account.Username, account.Username, "") });
        });

        // Launcher opens this in the browser.
        app.MapGet("/auth/login", (string? session_id, IConfiguration cfg) =>
        {
            if (!cfg.GetValue("Auth:EnableLocalLogin", true)) return Results.NotFound();
            if (!IsValidSessionId(session_id ?? "")) return Results.BadRequest("Invalid session ID");
            return Results.Content(LoginPage(session_id!), "text/html; charset=utf-8");
        });

        // Browser form submit from the page above.
        app.MapPost("/auth/login/submit", async (HttpContext ctx, AccountStore store,
            JwtService jwt, IMemoryCache cache, CancellationToken ct) =>
        {
            var form = await ctx.Request.ReadFormAsync(ct);
            var sid = form["session_id"].ToString();
            var name = form["username"].ToString().Trim();
            if (!IsValidSessionId(sid) || string.IsNullOrEmpty(name))
                return Results.BadRequest("Invalid session or username");
            var account = await store.GetOrCreateByUsernameAsync(name, ct);
            if (account is null) return Results.Conflict();
            var token = jwt.MintLocal(account.Username, name, "");
            cache.Set(HandoffPrefix + sid, token, HandoffTtl);
            return Results.Content(DonePage(name), "text/html; charset=utf-8");
        });

        // Frontend/legacy flow: park a token for a session_id.
        app.MapPost("/auth/token/submit", (SubmitRequest req, IMemoryCache cache) =>
        {
            if (!IsValidSessionId(req.session_id) || string.IsNullOrEmpty(req.token))
                return Results.BadRequest();
            cache.Set(HandoffPrefix + req.session_id, req.token, HandoffTtl);
            return Results.Json(new { success = true });
        });

        // Launcher polls this; one-shot read.
        app.MapPost("/auth/token/poll", (SessionRequest req, IMemoryCache cache) =>
        {
            if (!IsValidSessionId(req.session_id)) return Results.BadRequest();
            var key = HandoffPrefix + req.session_id;
            if (!cache.TryGetValue<string>(key, out var token) || token is null)
                return Results.Json(new { error = "Session ID not found" }, statusCode: 404);
            cache.Remove(key);
            return Results.Json(new LoginResponse { token = token });
        });

        return app;
    }

    internal static bool IsValidSessionId(string id) =>
        id.Length is >= 20 and <= 60 && id.All(char.IsAscii);

    private static string LoginPage(string sessionId)
    {
        var esc = System.Net.WebUtility.HtmlEncode(sessionId);
        return $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8"><title>Portable Lethe - Login</title>
            <style>body{background:#1a1a2e;color:#e0e0e0;font-family:system-ui;display:flex;align-items:center;
            justify-content:center;height:100vh;margin:0}.card{background:#24243e;padding:2rem;border-radius:8px;
            min-width:320px}input,button{width:100%;padding:.6rem;margin:.4rem 0;box-sizing:border-box}</style>
            </head><body><div class="card"><h2>Portable Lethe</h2>
            <form method="post" action="/auth/login/submit">
            <input type="hidden" name="session_id" value="{{esc}}">
            <label>Username<input name="username" required autofocus></label>
            <button type="submit">Sign in</button></form></div></body></html>
            """;
    }

    private static string DonePage(string name) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Logged in</title></head>"
        + "<body style=\"background:#1a1a2e;color:#e0e0e0;font-family:system-ui;text-align:center;padding:4rem\">"
        + "<h1>Signed in as " + System.Net.WebUtility.HtmlEncode(name) + "</h1>"
        + "<p>You can close this window and return to the launcher.</p></body></html>";
}
