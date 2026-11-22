using OpenLethe.Data;

namespace OpenLethe.Plugin.Abstractions;

/// Contract for global, admin-installed server plugins. Plugins are fully
/// trusted (they run as ordinary .NET code inside the server process).
public interface IPlugin
{
    string Id { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}

public interface IAccountCreatedHandler
{
    void OnAccountCreated(Account account);
}

public interface IPacketObserver
{
    /// Called for every authenticated game request. Observing only - responses
    /// are handled by the script interception layer.
    void OnPacket(string path, System.Text.Json.JsonElement parameters, string subject);
}
