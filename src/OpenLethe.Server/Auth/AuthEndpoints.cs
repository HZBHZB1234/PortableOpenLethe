using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using OpenLethe.Data;

namespace OpenLethe.Server.Auth;

/// Portable build: local login replacing Discord OAuth. The launcher handoff
/// shape is preserved: GET /auth/login?session_id=... opens the built Vue SPA,
/// the browser form-POSTs the username, and the launcher polls /auth/token/poll.
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

        // Launcher opens this in the browser. Serve the built SPA from wwwroot;
        // its query string stays intact so webui/auth/main.ts can read session_id.
        // Static hosting serves the same file at /auth/ (UseDefaultFiles).
        app.MapGet("/auth/login", (string? session_id, IConfiguration cfg, IWebHostEnvironment env) =>
        {
            if (!cfg.GetValue("Auth:EnableLocalLogin", true)) return Results.NotFound();
            if (!IsValidSessionId(session_id ?? "")) return Results.BadRequest("Invalid session ID");
            var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            var index = Path.Combine(webRoot, "auth", "index.html");
            if (!File.Exists(index))
                return Results.NotFound("Auth UI is not built. Run `npm run build` in webui/.");
            return Results.File(index, "text/html; charset=utf-8");
        });

        // Browser form submit from the SPA above.
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
            // The launcher never reads this response - it polls /auth/token/poll.
            // Redirect into the SPA's success route so the themed done page is
            // shown without any server-side HTML rendering (and with no raw
            // user input in markup).
            return Results.Redirect("/auth/#/done?name=" + Uri.EscapeDataString(name));
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
}
