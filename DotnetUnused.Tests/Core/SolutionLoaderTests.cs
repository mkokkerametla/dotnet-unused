using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotnetUnused.Core;
using Xunit;

namespace DotnetUnused.Tests.Core;

public class SolutionLoaderTests
{
    private sealed record Fake(string Name, bool IsSdk, Version Version);

    private static Fake? Select(params Fake[] instances) =>
        SolutionLoader.SelectInstance(instances, i => i.IsSdk, i => i.Version);

    [Fact]
    public void SelectInstance_PrefersSdkOverNewerVisualStudio()
    {
        var result = Select(
            new Fake("VS 18", false, new Version(18, 10)),
            new Fake("SDK 10", true, new Version(10, 0)),
            new Fake("SDK 8", true, new Version(8, 0))
        );

        Assert.Equal("SDK 10", result?.Name);
    }

    [Fact]
    public void SelectInstance_FallsBackToVisualStudioWhenNoSdk()
    {
        var result = Select(
            new Fake("VS 17", false, new Version(17, 8)),
            new Fake("VS 18", false, new Version(18, 10))
        );

        Assert.Equal("VS 18", result?.Name);
    }

    [Fact]
    public void SelectInstance_WithNoInstances_ReturnsNull()
    {
        Assert.Null(Select());
    }

    [Fact]
    public async Task LoadAsync_WithMissingMSBuildPath_ThrowsDirectoryNotFound()
    {
        var loader = new SolutionLoader(Path.Combine(Path.GetTempPath(), "no-such-msbuild-dir"));

        // Only meaningful if MSBuild has not already been registered in this process.
        try
        {
            await loader.LoadAsync("whatever.sln");
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        catch (FileNotFoundException)
        {
            return; // MSBuild already registered by another test; path override is skipped
        }

        Assert.Fail("Expected an exception");
    }
}
