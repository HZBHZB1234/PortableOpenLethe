using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;

namespace OpenLethe.Scripting;

/// Installed after JwtAuthMiddleware: user scripts may take over a request or
/// rewrite its JSON response. Users without scripts pay only a dictionary lookup.
public sealed class ScriptInterceptionMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions WireOptions = new() { IncludeFields = true };

    public async Task Invoke(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/", StringComparison.Ordinal)
            && !path.StartsWith("/login/", StringComparison.Ordinal))
        {
            await next(ctx);
            return;
        }
        if (ctx.Items["sub"] is not string sub)
        {
            await next(ctx);
            return;
        }

        var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Username == sub);
        if (account is null) { await next(ctx); return; }

        var runtimes = ScriptRegistry.LoadForAccount(db, account.Id);
        if (runtimes.Count == 0) { await next(ctx); return; }

        var logger = ctx.RequestServices.GetRequiredService<ILogger<ScriptInterceptionMiddleware>>();
        var runners = new List<ScriptRunner>();
        foreach (var rt in runtimes)
        {
            try
            {
                var runner = new ScriptRunner(rt, account, db, logger);
                runner.Execute();
                runners.Add(runner);
            }
            catch (Exception e) { logger.LogWarning(e, "Script {Name} failed to load", rt.Entity.Name); }
        }

        foreach (var runner in runners)
        {
            var rt = runner.Runtime;
            if (!rt.RequestInterceptors.TryGetValue(path, out var cb)) continue;
            var result = runner.Invoke(cb, Parse(ctx, runner, ctx.Items["packet.parameters"]));
            if (result is null || result == Jint.Native.JsValue.Undefined || result == Jint.Native.JsValue.Null) continue;
            var json = runner.Stringify(result);
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsJsonAsync(new
            {
                serverInfo = new { version = "product" },
                state = "ok",
                result = JsonSerializer.Deserialize<JsonElement>(json),
                packetId = 67L,
            }, WireOptions);
            return;
        }

        var modifiers = runners.Where(r => r.Runtime.ResponseInterceptors.ContainsKey(path)).ToList();
        if (modifiers.Count == 0) { await next(ctx); return; }

        // Buffer the response so scripts can rewrite the JSON body.
        var originalBody = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try
        {
            await next(ctx);
            ctx.Response.Body = originalBody;
            var bytes = buffer.ToArray();
            if (bytes.Length > 0 && ctx.Response.StatusCode == 200)
            {
                try
                {
                    using var doc = JsonDocument.Parse(bytes);
                    var current = Parse(ctx, modifiers[0], doc.RootElement.GetRawText());
                    foreach (var runner in modifiers)
                    {
                        var cb = runner.Runtime.ResponseInterceptors[path];
                        var result = runner.Invoke(cb, current);
                        if (result is not null && result != Jint.Native.JsValue.Undefined && result != Jint.Native.JsValue.Null)
                            current = Parse(ctx, runner, runner.Stringify(result));
                    }
                    bytes = JsonSerializer.SerializeToUtf8Bytes(current, WireOptions);
                }
                catch (JsonException) { /* leave binary/non-JSON responses untouched */ }
            }
            ctx.Response.ContentLength = bytes.Length;
            await originalBody.WriteAsync(bytes);
        }
        finally { ctx.Response.Body = originalBody; }
    }

    private static Jint.Native.JsValue Parse(HttpContext ctx, ScriptRunner runner, object? raw)
    {
        var json = raw switch
        {
            JsonElement el => el.GetRawText(),
            string s => s,
            null => "{}",
            _ => JsonSerializer.Serialize(raw, WireOptions),
        };
        return runner.ParseJsonValue(json);
    }
}

public static class ScriptMiddlewareExtensions
{
    public static IApplicationBuilder UseScriptInterception(this IApplicationBuilder app) =>
        app.UseMiddleware<ScriptInterceptionMiddleware>();
}




