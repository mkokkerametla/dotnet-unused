using DotnetUnused.Core;
using DotnetUnused.Models;
using Xunit;

namespace DotnetUnused.Tests.Core;

public class UnusedPackageDetectorTests
{
    private readonly UnusedPackageDetector _detector = new();

    [Fact]
    public void DetectUnused_ReturnsUnusedPackages()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "Newtonsoft.Json",
                Version = "13.0.3",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            },
            new()
            {
                PackageId = "Serilog",
                Version = "3.1.1",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        // Only Newtonsoft.Json is used
        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Newtonsoft.Json",
            "Newtonsoft.Json.Linq"
        };

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert
        Assert.Single(unused);
        Assert.Equal("Serilog", unused[0].PackageId);
    }

    [Fact]
    public void DetectUnused_ExcludesBuildPackages()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "Microsoft.NET.Test.Sdk",
                Version = "17.8.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            },
            new()
            {
                PackageId = "coverlet.collector",
                Version = "6.0.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            },
            new()
            {
                PackageId = "xunit.runner.visualstudio",
                Version = "2.5.5",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            },
            new()
            {
                PackageId = "StyleCop.Analyzers",
                Version = "1.1.118",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert - build packages should not be flagged as unused
        Assert.Empty(unused);
    }

    [Fact]
    public void DetectUnused_ExcludesAnalyzerPackages()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "MyCompany.Analyzers",
                Version = "1.0.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            },
            new()
            {
                PackageId = "SonarAnalyzer.CSharp",
                Version = "9.15.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert - analyzer packages should be excluded
        Assert.Empty(unused);
    }

    [Fact]
    public void DetectUnused_IncludesAllPackageMetadata()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "UnusedLib",
                Version = "1.0.0",
                ProjectName = "MyProject",
                ProjectPath = "C:\\Projects\\MyProject.csproj"
            }
        };

        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert
        Assert.Single(unused);
        Assert.Equal("UnusedLib", unused[0].PackageId);
        Assert.Equal("1.0.0", unused[0].Version);
        Assert.Equal("MyProject", unused[0].ProjectName);
        Assert.Equal("C:\\Projects\\MyProject.csproj", unused[0].ProjectPath);
        Assert.NotEmpty(unused[0].CandidateNamespaces);
    }

    [Fact]
    public void DetectUnused_MatchesPartialNamespaces()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "Microsoft.Extensions.Logging",
                Version = "8.0.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        // Using a child namespace should count as usage
        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.Extensions.Logging.Abstractions"
        };

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert - package should be considered used
        Assert.Empty(unused);
    }

    [Fact]
    public void DetectUnused_MatchesPrefixNamespaces()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "Newtonsoft.Json",
                Version = "13.0.3",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        // Using just the prefix namespace
        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Newtonsoft"
        };

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert - should be considered used (prefix matches)
        Assert.Empty(unused);
    }

    [Fact]
    public void DetectUnused_ReturnsEmptyWhenAllUsed()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "Newtonsoft.Json",
                Version = "13.0.3",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            },
            new()
            {
                PackageId = "Serilog",
                Version = "3.1.1",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Newtonsoft.Json",
            "Serilog",
            "Serilog.Events"
        };

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert
        Assert.Empty(unused);
    }

    [Fact]
    public void DetectUnused_ReturnsEmptyForEmptyInput()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>();
        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert
        Assert.Empty(unused);
    }

    [Fact]
    public void DetectUnused_ExcludesGeneratorPackages()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "MyLibrary.SourceGenerator",
                Version = "1.0.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            },
            new()
            {
                PackageId = "MyLibrary.Generators",
                Version = "1.0.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert - generator packages should be excluded
        Assert.Empty(unused);
    }

    [Fact]
    public void DetectUnused_ExcludesRuntimePackages()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "runtime.native.System",
                Version = "4.3.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            },
            new()
            {
                PackageId = "runtime.native.System.IO.Compression",
                Version = "4.3.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert - runtime packages should be excluded
        Assert.Empty(unused);
    }

    [Fact]
    public void DetectUnused_IsCaseInsensitive()
    {
        // Arrange
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "NEWTONSOFT.JSON",
                Version = "13.0.3",
                ProjectName = "Test",
                ProjectPath = "Test.csproj"
            }
        };

        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "newtonsoft.json"
        };

        // Act
        var unused = _detector.DetectUnused(packages, foundNamespaces);

        // Assert - case-insensitive matching
        Assert.Empty(unused);
    }
}
