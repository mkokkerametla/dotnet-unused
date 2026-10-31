using System.IO.Pipelines;
using DotnetUnused.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DotnetUnused.Tests.Mcp;

/// <summary>
/// Talks to the real server configuration over in-memory pipes. Analysis itself needs
/// MSBuildWorkspace, so these cover the protocol surface and error handling.
/// </summary>
public class McpServerTests : IAsyncLifetime
{
    private IHost _host = null!;
    private McpClient _client = null!;

    public async Task InitializeAsync()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateApplicationBuilder();
        builder
            .Services.AddDotnetUnusedMcpServer(new McpServerSettings(MSBuildPath: null))
            .WithStreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream()
            );
        _host = builder.Build();
        await _host.StartAsync();

        _client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream()
            )
        );
    }

    public async Task DisposeAsync()
    {
        await _client.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task ListsToolsWithSafetyAnnotations()
    {
        var tools = (await _client.ListToolsAsync()).ToDictionary(t => t.Name);

        Assert.Equal(["analyze_solution", "fix_unused_usings"], tools.Keys.Order());

        var analyze = tools["analyze_solution"].ProtocolTool;
        Assert.True(analyze.Annotations?.ReadOnlyHint);
        Assert.NotNull(analyze.OutputSchema);

        // Clients use the destructive hint to ask the user before the tool runs
        var fix = tools["fix_unused_usings"].ProtocolTool;
        Assert.True(fix.Annotations?.DestructiveHint);
        Assert.NotEqual(true, fix.Annotations?.ReadOnlyHint);
    }

    [Fact]
    public async Task MissingPathIsReportedAsToolError()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "App.sln");

        var result = await _client.CallToolAsync(
            "analyze_solution",
            new Dictionary<string, object?> { ["path"] = missing }
        );

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Path not found", text);
    }
}
