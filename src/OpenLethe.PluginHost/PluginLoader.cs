using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using OpenLethe.Plugin.Abstractions;

namespace OpenLethe.PluginHost;

public sealed class PluginManifest
{
    public string id { get; set; } = "";
    public string version { get; set; } = "0.0.0";
    public string entry { get; set; } = "";
    public string minServerVersion { get; set; } = "";
    public string[] permissions { get; set; } = [];
}

public sealed class PluginLoadContext(string pluginAssemblyPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Shared contracts must come from the default context or IPlugin type
        // identity splits and casting fails.
        if (assemblyName.Name is "OpenLethe.Plugin.Abstractions" or "OpenLethe.Data"
            || assemblyName.Name?.StartsWith("Microsoft.Extensions.") == true
            || assemblyName.Name?.StartsWith("System.") == true)
            return null;
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}

public sealed class LoadedPlugin
{
    public required string Id { get; init; }
    public required IPlugin Instance { get; init; }
    public required PluginLoadContext Context { get; init; }
}

/// Discovers plugins under <root>/<id>/plugin.json and loads their entry DLL.
/// A broken plugin is logged and skipped; it never prevents server startup.
public static class PluginLoader
{
    public static List<LoadedPlugin> Load(string root, ILogger logger)
    {
        var result = new List<LoadedPlugin>();
        if (!Directory.Exists(root)) return result;

        foreach (var dir in Directory.EnumerateDirectories(root).OrderBy(d => d))
        {
            var manifestPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(manifestPath)) continue;
            PluginManifest? manifest;
            try { manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath)); }
            catch (Exception e) { logger.LogWarning(e, "Plugin manifest invalid: {Path}", manifestPath); continue; }
            if (manifest is null || string.IsNullOrEmpty(manifest.entry)) continue;

            var dll = Path.Combine(dir, manifest.entry);
            if (!File.Exists(dll)) { logger.LogWarning("Plugin entry missing: {Dll}", dll); continue; }

            try
            {
                var alc = new PluginLoadContext(dll);
                var asm = alc.LoadFromAssemblyPath(dll);
                var type = asm.GetTypes().FirstOrDefault(t =>
                    typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
                if (type is null) { logger.LogWarning("No IPlugin in {Dll}", dll); alc.Unload(); continue; }
                var instance = (IPlugin)(Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException("Activator returned null"));
                result.Add(new LoadedPlugin { Id = manifest.id, Instance = instance, Context = alc });
                logger.LogInformation("Plugin loaded: {Id} {Version}", manifest.id, manifest.version);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Plugin failed to load: {Dll}", dll);
            }
        }
        return result;
    }
}
