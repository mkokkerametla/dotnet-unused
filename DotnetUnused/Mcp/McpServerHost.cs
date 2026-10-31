using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace DotnetUnused.Mcp;

/// <summary>
/// Runs dotnet-unused as a Model Context Protocol server over stdio (<c>dotnet-unused mcp</c>)
/// </summary>
public static class McpServerHost
{
    public static async Task RunAsync(
        McpServerSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        // stdout carries the JSON-RPC stream (the transport writes to the raw stdout stream).
        // Send anything else written through Console, including from libraries, to stderr so
        // it cannot corrupt the protocol.
        Console.SetOut(Console.Error);

        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory }
        );
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddDotnetUnusedMcpServer(settings).WithStdioServerTransport();

        await builder.Build().RunAsync(cancellationToken);
    }

    /// <summary>
    /// Registers the server and its tools, leaving the transport to the caller
    /// </summary>
    public static IMcpServerBuilder AddDotnetUnusedMcpServer(
        this IServiceCollection services,
        McpServerSettings settings
    )
    {
        services.AddSingleton(settings);
        return services
            .AddMcpServer(options =>
                options.ServerInfo = new Implementation
                {
                    Name = "dotnet-unused",
                    Version = GetVersion(),
                }
            )
            .WithTools<UnusedCodeTools>();
    }

    private static string GetVersion()
    {
        var version = typeof(McpServerHost)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        // Strip the "+<commit sha>" build metadata the SDK appends
        return version?.Split('+')[0] ?? "0.0.0";
    }
}
