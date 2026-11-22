using System.Text.Json;
using Jint;
using Jint.Native;
using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;

namespace OpenLethe.Scripting;

public static class Caps
{
    public const string Log = "log";
    public const string ReadSelf = "account.read:self";
    public const string WriteSelf = "account.write:self";
    public const string Endpoint = "endpoint";
    public const string PacketRead = "packet.read";
    public const string Intercept = "packet.intercept";
    public const string Modify = "packet.modify";
    public const string Timer = "timer";
    public const string ConfigRead = "config.read";
    public const string Http = "http";
    public static readonly string[] Baseline = [Log, ReadSelf, WriteSelf, Endpoint, ConfigRead];
}

public sealed class ScriptHeader
{
    public string id { get; set; } = "";
    public string version { get; set; } = "0.0.0";
    public string[] permissions { get; set; } = [];
}

public sealed class ScriptRuntime
{
    public required UserScript Entity { get; init; }
    public required ScriptHeader Header { get; init; }
    public required HashSet<string> Capabilities { get; init; }
    public Dictionary<string, Func<JsValue, JsValue[], JsValue>> Actions { get; } = new();
    public Dictionary<string, Func<JsValue, JsValue[], JsValue>> RequestInterceptors { get; } = new();
    public Dictionary<string, Func<JsValue, JsValue[], JsValue>> ResponseInterceptors { get; } = new();
    public List<(string Event, Func<JsValue, JsValue[], JsValue> Handler)> EventHandlers { get; } = new();
}

/// Parses the /* { ... } */ header and builds the effective capability set:
/// header declarations intersected with admin grants (baseline always allowed).
public static class ScriptRegistry
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static ScriptHeader ParseHeader(string source)
    {
        var start = source.IndexOf("/*", StringComparison.Ordinal);
        var end = source.IndexOf("*/", StringComparison.Ordinal);
        if (start < 0 || end <= start) return new ScriptHeader();
        try { return JsonSerializer.Deserialize<ScriptHeader>(source[(start + 2)..end], JsonOpts) ?? new ScriptHeader(); }
        catch { return new ScriptHeader(); }
    }

    public static List<ScriptRuntime> LoadForAccount(AppDbContext db, Guid accountId)
    {
        var scripts = db.UserScripts.AsNoTracking()
            .Where(s => s.AccountId == accountId && s.Enabled).ToList();
        var grants = db.ScriptGrants.AsNoTracking()
            .Where(g => g.GranteeAccountId == accountId).ToList();

        var result = new List<ScriptRuntime>();
        foreach (var s in scripts)
        {
            var header = ParseHeader(s.Source);
            var granted = grants.FirstOrDefault(g => g.ScriptId == s.Id);
            HashSet<string> extra = [];
            if (granted is not null)
            {
                try { extra = JsonSerializer.Deserialize<string[]>(granted.Capabilities)?.ToHashSet() ?? []; }
                catch { /* ignore malformed grant */ }
            }
            var effective = new HashSet<string>(Caps.Baseline);
            foreach (var cap in header.permissions)
                if (extra.Contains(cap) || Caps.Baseline.Contains(cap)) effective.Add(cap);
            result.Add(new ScriptRuntime { Entity = s, Header = header, Capabilities = effective });
        }
        return result;
    }
}

/// Executes a script with a restricted host facade. No CLR type resolution,
/// file system, require/import, or eval is exposed to the script.
public sealed class ScriptRunner
{
    private readonly Engine _engine;
    private readonly ScriptRuntime _runtime;

    public ScriptRunner(ScriptRuntime runtime, Account account, AppDbContext db, ILogger logger)
    {
        _runtime = runtime;
        _engine = new Engine(opts => opts.LimitMemory(16 * 1024 * 1024));
        var cap = runtime.Capabilities;

        _engine.SetValue("Log", new
        {
            info = new Action<string>(m => logger.LogInformation("[script:{Id}] {Msg}", runtime.Entity.Name, m)),
            warn = new Action<string>(m => logger.LogWarning("[script:{Id}] {Msg}", runtime.Entity.Name, m)),
            error = new Action<string>(m => logger.LogError("[script:{Id}] {Msg}", runtime.Entity.Name, m)),
        });

        _engine.SetValue("Account", new
        {
            get = new Func<string, JsValue>(doc => ParseJson(ReadDoc(account, doc, cap))),
            setField = new Action<string, JsValue>((doc, value) =>
            {
                if (!cap.Contains(Caps.WriteSelf)) throw new UnauthorizedAccessException("account.write:self");
                var prop = typeof(Account).GetProperty(doc) ?? throw new InvalidOperationException("unknown field " + doc);
                prop.SetValue(account, value.IsString() ? value.AsString() : value.ToString());
                account.UpdatedAt = DateTime.UtcNow;
                db.SaveChanges();
            }),
        });

        _engine.SetValue("Hub", new
        {
            on = new Action<string, Func<JsValue, JsValue[], JsValue>>((ev, fn) =>
                runtime.EventHandlers.Add((ev, fn))),
            action = new Action<string, Func<JsValue, JsValue[], JsValue>>((name, fn) =>
            {
                if (!cap.Contains(Caps.Endpoint)) throw new UnauthorizedAccessException("endpoint");
                runtime.Actions[name] = fn;
            }),
            intercept = new
            {
                request = new Action<string, Func<JsValue, JsValue[], JsValue>>((path, fn) =>
                {
                    if (!cap.Contains(Caps.Intercept)) throw new UnauthorizedAccessException("packet.intercept");
                    runtime.RequestInterceptors[path] = fn;
                }),
                response = new Action<string, Func<JsValue, JsValue[], JsValue>>((path, fn) =>
                {
                    if (!cap.Contains(Caps.Modify)) throw new UnauthorizedAccessException("packet.modify");
                    runtime.ResponseInterceptors[path] = fn;
                }),
            },
        });

        _engine.SetValue("Config", new
        {
            get = new Func<string, string?>(key => null), // no non-secret keys exposed in v1
        });
    }

    public void Execute()
    {
        _engine.Execute(_runtime.Entity.Source);
        foreach (var (ev, fn) in _runtime.EventHandlers.ToList())
            if (ev == "accountLoaded") Invoke(fn, JsValue.Undefined);
    }

    public ScriptRuntime Runtime => _runtime;
    public JsValue ParseJsonValue(string json) => ParseJson(json);
    public string Stringify(JsValue value) => _engine.Invoke("JSON.stringify", value).AsString();

    public JsValue? Invoke(Func<JsValue, JsValue[], JsValue> fn, JsValue arg)
    {
        try { return fn(JsValue.Undefined, [arg]); }
        catch (Exception e) { _engine.SetValue("__lastError", e.Message); return null; }
    }

    private static string? ReadDoc(Account account, string doc, HashSet<string> cap)
    {
        if (!cap.Contains(Caps.ReadSelf)) throw new UnauthorizedAccessException("account.read:self");
        var prop = typeof(Account).GetProperty(doc) ?? throw new InvalidOperationException("unknown field " + doc);
        var json = prop.GetValue(account) as string;
        if (string.IsNullOrWhiteSpace(json) || json is "{}" or "[]" or "null") return null;
        return json;
    }

    private JsValue ParseJson(string? json)
    {
        if (json is null) return JsValue.Null;
        return _engine.Invoke("JSON.parse", json);
    }
}
