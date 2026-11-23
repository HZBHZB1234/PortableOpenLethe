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
            <!DOCTYPE html>
            <html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Portable Lethe · 登录</title>
            <style>
              :root{--canvas:#f8fafc;--surface:#fff;--border:#e2e8f0;--strong:#cbd5e1;--text:#0f172a;
                --muted:#475569;--faint:#94a3b8;--accent:#5e6ad2;--accent-hover:#4a56c0;--soft:#eef0fb}
              *{box-sizing:border-box}
              body{margin:0;min-height:100vh;background:var(--canvas);color:var(--text);
                font:14px/1.5 "Inter","Geist",-apple-system,BlinkMacSystemFont,"Segoe UI",system-ui,sans-serif;
                display:grid;place-items:center;padding:24px;-webkit-font-smoothing:antialiased}
              .card{width:100%;max-width:392px;background:var(--surface);border:1px solid var(--border);
                border-radius:12px;padding:32px;box-shadow:0 4px 6px -2px rgba(15,23,42,.05),0 10px 15px -3px rgba(15,23,42,.08)}
              .brand{display:flex;align-items:center;gap:12px;margin-bottom:28px}
              .mark{width:36px;height:36px;border-radius:8px;background:var(--accent);color:#fff;display:grid;
                place-items:center;font-weight:600;font-size:14px;letter-spacing:-.02em}
              .name{font-size:15px;font-weight:600}.sub{font-size:12px;color:var(--faint)}
              h1{font-size:18px;font-weight:600;letter-spacing:-.01em;margin:0 0 6px}
              p{font-size:13px;color:var(--muted);margin:0 0 24px}
              label{display:block;font-size:12px;font-weight:500;color:var(--muted);margin-bottom:16px}
              input{display:block;width:100%;margin-top:6px;padding:9px 12px;border:1px solid var(--strong);
                border-radius:6px;background:var(--surface);color:var(--text);font:inherit;outline:none;
                transition:border-color .15s ease-out,box-shadow .15s ease-out}
              input:focus{border-color:var(--accent);box-shadow:0 0 0 3px rgba(94,106,210,.18)}
              button{width:100%;height:38px;margin-top:4px;border:0;border-radius:6px;background:var(--accent);
                color:#fff;font:500 13px inherit;cursor:pointer;transition:background-color .15s ease-out}
              button:hover{background:var(--accent-hover)}
              .foot{margin-top:20px;padding-top:16px;border-top:1px solid var(--border);font-size:12px;color:var(--faint)}
            </style></head>
            <body><div class="card">
              <div class="brand"><div class="mark">PL</div><div><div class="name">Portable Lethe</div><div class="sub">Local private server</div></div></div>
              <h1>登录私服</h1>
              <p>输入用户名以继续。首次登录将自动创建本地账号。</p>
              <form method="post" action="/auth/login/submit">
                <input type="hidden" name="session_id" value="{{esc}}">
                <label>用户名<input name="username" required autofocus autocomplete="username" placeholder="username"></label>
                <button type="submit">Sign in</button>
              </form>
              <div class="foot">此页面由本地服务端提供 · 登录完成后可关闭窗口返回 Launcher</div>
            </div></body></html>
            """;
    }

    private static string DonePage(string name)
    {
        var esc = System.Net.WebUtility.HtmlEncode(name);
        return $$"""
            <!DOCTYPE html>
            <html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Portable Lethe · 已登录</title>
            <style>
              :root{--canvas:#f8fafc;--surface:#fff;--border:#e2e8f0;--text:#0f172a;--muted:#475569;
                --ok:#15803d;--ok-bg:#dcfce7;--ok-bd:#86efac}
              *{box-sizing:border-box}
              body{margin:0;min-height:100vh;background:var(--canvas);color:var(--text);display:grid;place-items:center;
                padding:24px;font:14px/1.5 "Inter","Geist",-apple-system,BlinkMacSystemFont,"Segoe UI",system-ui,sans-serif}
              .card{width:100%;max-width:392px;background:var(--surface);border:1px solid var(--border);border-radius:12px;
                padding:32px;text-align:center;box-shadow:0 4px 6px -2px rgba(15,23,42,.05),0 10px 15px -3px rgba(15,23,42,.08)}
              .check{width:44px;height:44px;border-radius:50%;background:var(--ok-bg);border:1px solid var(--ok-bd);
                color:var(--ok);display:grid;place-items:center;margin:0 auto 18px;font-size:22px}
              h1{font-size:18px;font-weight:600;margin:0 0 8px}
              p{font-size:13px;color:var(--muted);margin:0}
              .user{display:inline-flex;margin-top:18px;padding:3px 10px;border:1px solid var(--border);
                border-radius:4px;background:#f8fafc;font:12px ui-monospace,monospace;color:var(--text)}
            </style></head>
            <body><div class="card">
              <div class="check">✓</div>
              <h1>登录成功</h1>
              <p>你可以关闭此窗口并返回 Launcher 继续游戏。</p>
              <div class="user">{{esc}}</div>
            </div></body></html>
            """;
    }
}
