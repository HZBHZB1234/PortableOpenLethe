using System.Text.Json;

namespace OpenLethe.Resources;

/// Portable build: all static data is read from a directory at runtime.
///
/// Root resolution (first hit wins):
///   1. STATIC_DATA_DIR environment variable
///   2. &lt;AppContext.BaseDirectory&gt;/static  (published layout)
///
/// Layout under the root:
///   StaticData/   -- logical names are relative paths with forward slashes,
///                    e.g. "static-data/ego/ego-1.json"
///   Localize/     -- e.g. "en/EN_Personalities.json"
///
/// The Hub replaces the root contents when syncing official resources; changes
/// take effect after a server restart (handlers memoize parsed tables).
public static class StaticData
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    public static string Root =>
        Environment.GetEnvironmentVariable("STATIC_DATA_DIR")
        ?? Path.Combine(AppContext.BaseDirectory, "static");

    private static string StaticRoot => Path.Combine(Root, "StaticData");
    private static string LocalizeRoot => Path.Combine(Root, "Localize");

    private static string Physical(string logical)
    {
        var norm = logical.Replace('\\', '/').TrimStart('/');
        var isLocalize = norm.StartsWith("Localize/", StringComparison.OrdinalIgnoreCase);
        var baseDir = isLocalize ? LocalizeRoot : StaticRoot;
        if (isLocalize) norm = norm["Localize/".Length..];
        return Path.Combine(baseDir, norm.Replace('/', Path.DirectorySeparatorChar));
    }

    private static IEnumerable<string> Enumerate(string baseDir, string prefix)
    {
        if (!Directory.Exists(baseDir)) return [];
        var normPrefix = prefix.Replace('\\', '/').Trim('/');
        return Directory.EnumerateFiles(baseDir, "*.json", SearchOption.AllDirectories)
            .Select(p => new { Physical = p, Logical = Path.GetRelativePath(baseDir, p).Replace('\\', '/') })
            .Where(x => normPrefix.Length == 0
                || x.Logical.StartsWith(normPrefix + "/", StringComparison.OrdinalIgnoreCase)
                || x.Logical.StartsWith(normPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Physical);
    }

    private static T? Read<T>(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return default; // mirror Rust: warn + skip bad files
        }
    }

    public static List<T> GetList<T>(string folder)
    {
        var result = new List<T>();
        foreach (var path in Enumerate(StaticRoot, folder))
        {
            var doc = Read<ValList<T>>(path);
            if (doc?.list is not null) result.AddRange(doc.list);
        }
        return result;
    }

    public static List<T> GetListFromFile<T>(string resourcePath)
    {
        var doc = Read<ValList<T>>(Physical(resourcePath));
        return doc?.list ?? [];
    }

    public static List<T> GetUnlisted<T>(string folder)
    {
        var result = new List<T>();
        foreach (var path in Enumerate(StaticRoot, folder))
        {
            var doc = Read<T>(path);
            if (doc is not null) result.Add(doc);
        }
        return result;
    }

    public static List<T> GetLocalizeList<T>(string resourcePath)
    {
        var doc = Read<DataList<T>>(Physical("Localize/" + resourcePath));
        return doc?.dataList ?? [];
    }

    public static List<T> GetLocalizeListByPrefix<T>(string prefix)
    {
        var result = new List<T>();
        var full = "Localize/" + prefix.Replace('\\', '/').TrimStart('/');
        if (full.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var single = Read<DataList<T>>(Physical(full));
            if (single?.dataList is not null) result.AddRange(single.dataList);
            return result;
        }
        var path = Physical(full);
        var dir = Path.GetDirectoryName(path)!;
        var filePrefix = Path.GetFileName(path);
        if (!Directory.Exists(dir)) return result;
        foreach (var file in Directory.EnumerateFiles(dir, filePrefix + "*.json"))
        {
            var doc = Read<DataList<T>>(file);
            if (doc?.dataList is not null) result.AddRange(doc.dataList);
        }
        return result;
    }

    private sealed class ValList<T>
    {
        public List<T>? list;
    }

    private sealed class DataList<T>
    {
        public List<T>? dataList;
    }
}

/// { "id": &lt;n&gt; } — the shape most static-data entries share.
public sealed class IdStruct
{
    public long id;
}

/// { "nodeid": &lt;n&gt; } — shape of stagenodereward entries.
public sealed class NodeIdStruct
{
    public long nodeid;
}
