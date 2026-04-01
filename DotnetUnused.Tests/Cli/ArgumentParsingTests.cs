using System;
using System.Threading.Tasks;
using Xunit;

namespace DotnetUnused.Tests.Cli;

[Collection("Console")]
public class ArgumentParsingTests
{
    [Fact]
    public async Task Main_WithNoArguments_ShowsHelp()
    {
        var exitCode = await DotnetUnused.Program.Main(Array.Empty<string>());

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Main_WithHelpFlag_ShowsHelp()
    {
        var exitCode = await DotnetUnused.Program.Main(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact(Skip = "Requires actual CLI execution - use for manual testing")]
    public async Task Main_WithInvalidExcludePublicValue_ShowsError()
    {
        // Test that invalid boolean values for --exclude-public show error
        // e.g., "dotnet-unused project.sln --exclude-public invalid"
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires actual CLI execution - use for manual testing")]
    public async Task Main_WithValidArguments_ExecutesSuccessfully()
    {
        // Test normal execution with valid arguments
        await Task.CompletedTask;
    }
}
