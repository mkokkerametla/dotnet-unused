using DotnetUnused.Core;
using Xunit;

namespace DotnetUnused.Tests.Core;

public class PackageReferenceExtractorTests
{
    private readonly PackageReferenceExtractor _extractor = new();

    [Fact]
    public async Task ExtractFromProjectAsync_ExtractsPackageReferences()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "Test.csproj");
            var projectContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                    <PackageReference Include="Serilog" Version="3.1.1" />
                  </ItemGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(projectPath, projectContent);

            // Act
            var packages = await _extractor.ExtractFromProjectAsync(projectPath, "Test");

            // Assert
            Assert.Equal(2, packages.Count);
            Assert.Contains(
                packages,
                p => p.PackageId == "Newtonsoft.Json" && p.Version == "13.0.3"
            );
            Assert.Contains(packages, p => p.PackageId == "Serilog" && p.Version == "3.1.1");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExtractFromProjectAsync_HandlesVersionAsElement()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "Test.csproj");
            var projectContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Moq">
                      <Version>4.20.70</Version>
                    </PackageReference>
                  </ItemGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(projectPath, projectContent);

            // Act
            var packages = await _extractor.ExtractFromProjectAsync(projectPath, "Test");

            // Assert
            Assert.Single(packages);
            Assert.Equal("Moq", packages[0].PackageId);
            Assert.Equal("4.20.70", packages[0].Version);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExtractFromProjectAsync_HandlesCentralPackageManagement()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "Test.csproj");
            var projectContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="FluentAssertions" />
                  </ItemGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(projectPath, projectContent);

            var centralVersions = new Dictionary<string, string>
            {
                ["FluentAssertions"] = "6.12.0"
            };

            // Act
            var packages = await _extractor.ExtractFromProjectAsync(
                projectPath,
                "Test",
                centralVersions
            );

            // Assert
            Assert.Single(packages);
            Assert.Equal("FluentAssertions", packages[0].PackageId);
            Assert.Equal("6.12.0", packages[0].Version);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExtractFromProjectAsync_HandlesEmptyProject()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "Test.csproj");
            var projectContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(projectPath, projectContent);

            // Act
            var packages = await _extractor.ExtractFromProjectAsync(projectPath, "Test");

            // Assert
            Assert.Empty(packages);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExtractFromProjectAsync_MarksBuildOnlyPackages_AsNoCompileAssets()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "Test.csproj");
            var projectContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                    <PackageReference Include="DotNet.ReproducibleBuilds" Version="1.1.1">
                      <PrivateAssets>all</PrivateAssets>
                      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                    </PackageReference>
                    <PackageReference Include="SomeTool" Version="1.0.0" ExcludeAssets="compile" />
                  </ItemGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(projectPath, projectContent);

            // Act
            var packages = await _extractor.ExtractFromProjectAsync(projectPath, "Test");

            // Assert
            Assert.Equal(3, packages.Count);
            Assert.True(
                packages.Single(p => p.PackageId == "Newtonsoft.Json").IncludesCompileAssets
            );
            Assert.False(
                packages
                    .Single(p => p.PackageId == "DotNet.ReproducibleBuilds")
                    .IncludesCompileAssets
            );
            Assert.False(packages.Single(p => p.PackageId == "SomeTool").IncludesCompileAssets);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExtractFromProjectAsync_SetsProjectMetadata()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectPath = Path.Combine(tempDir, "MyApp.csproj");
            var projectContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="xunit" Version="2.6.2" />
                  </ItemGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(projectPath, projectContent);

            // Act
            var packages = await _extractor.ExtractFromProjectAsync(projectPath, "MyApp");

            // Assert
            Assert.Single(packages);
            Assert.Equal("MyApp", packages[0].ProjectName);
            Assert.Equal(projectPath, packages[0].ProjectPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
