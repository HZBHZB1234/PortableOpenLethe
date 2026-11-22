using System.Diagnostics;
using System.Net;

namespace OpenLethe.Server.Handlers;

/// Control surface used by LetheHub. Loopback only, token from `Hub:Token`
/// (falls back to the generated admin secret). The Hub discovers a restart by
/// polling this endpoint; the server itself never spawns processes.
public static class HubEndpoints
{
    public static IEndpointRouteBuilder MapHub(this IEndpointRouteBuilder app)
    {
        app.MapGet("/hub/status", (HttpContext ctx, IConfiguration cfg) =>
        {
            if (!Authorized(ctx, cfg)) return Results.StatusCode(403);
            return Results.Json(new
            {
                ok = true,
                startedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("O"),
                pid = Environment.ProcessId,
                staticDataRoot = Environment.GetEnvironmentVariable("STATIC_DATA_DIR") ?? "static",
                restartRequested = File.Exists(RestartMarker)
            });
        });

        app.MapGet("/hub/config/schema", (HttpContext ctx, IConfiguration cfg) =>
        {
            if (!Authorized(ctx, cfg)) return Results.StatusCode(403);
            var path = Path.Combine(AppContext.BaseDirectory, "config-schema.json");
            if (!File.Exists(path)) path = Path.Combine(Directory.GetCurrentDirectory(), "config-schema.json");
            return File.Exists(path)
                ? Results.Content(File.ReadAllText(path), "application/json")
                : Results.NotFound();
        });

        app.MapPost("/hub/request-restart", (HttpContext ctx, IConfiguration cfg) =>
        {
            if (!Authorized(ctx, cfg)) return Results.StatusCode(403);
            Directory.CreateDirectory(Path.GetDirectoryName(RestartMarker)!);
            File.WriteAllText(RestartMarker, DateTime.UtcNow.ToString("O"));
            return Results.Json(new { ok = true });
        });

        return app;
    }

    private static string RestartMarker =>
        Path.Combine(AppContext.BaseDirectory, "data", "restart.requested");

    private static bool Authorized(HttpContext ctx, IConfiguration cfg)
    {
        if (!IPAddress.IsLoopback(ctx.Connection.RemoteIpAddress ?? IPAddress.None)) return false;
        var expected = cfg["Hub:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            var secretPath = Path.Combine(AppContext.BaseDirectory, "data", "admin.secret");
            expected = File.Exists(secretPath) ? File.ReadAllText(secretPath).Trim() : "";
        }
        if (string.IsNullOrEmpty(expected)) return false;
        var provided = ctx.Request.Headers["X-Hub-Token"].ToString();
        return provided == expected;
    }
}
