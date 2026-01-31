using DotnetUnused.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DotnetUnused.Tests.Core;

public class UnusedUsingAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_UsesCS8019Fallback_WhenNoIDEAnalyzersAvailable()
    {
        // Arrange - Create an in-memory project (won't have IDE analyzers, but has compiler)
        var sourceCode =
            @"
using System;
using System.Linq;
using System.Collections.Generic;

namespace TestProject
{
    public class TestClass
    {
        public void TestMethod()
        {
            var list = new List<int>();
            Console.WriteLine(""Hello"");
        }
    }
}";
        var solution = CreateTestSolution("TestProject", "TestClass.cs", sourceCode);
        var analyzer = new UnusedUsingAnalyzer();

        // Act
        var results = await analyzer.AnalyzeAsync(solution);

        // Assert - Now falls back to CS8019 compiler diagnostic, so it finds unused usings
        Assert.NotNull(results);
        // Should detect System.Linq as unused (CS8019)
        Assert.Contains(results, r => r.Namespace.Contains("Linq"));
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNonNull_WithValidSolution()
    {
        // Arrange - Create code where all usings are used
        var sourceCode =
            @"
using System;
using System.Collections.Generic;

namespace TestProject
{
    public class TestClass
    {
        public void TestMethod()
        {
            var list = new List<int>();
            Console.WriteLine(""Hello"");
        }
    }
}";
        var solution = CreateTestSolution("TestProject", "TestClass.cs", sourceCode);
        var analyzer = new UnusedUsingAnalyzer();

        // Act
        var results = await analyzer.AnalyzeAsync(solution);

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    public async Task AnalyzeAsync_HandlesCancellation()
    {
        // Arrange
        var sourceCode =
            @"using System;
namespace TestProject { public class TestClass { } }";
        var solution = CreateTestSolution("TestProject", "TestClass.cs", sourceCode);
        var analyzer = new UnusedUsingAnalyzer();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await analyzer.AnalyzeAsync(solution, cancellationToken: cts.Token)
        );
    }

    [Fact(Skip = "Requires MSBuildWorkspace for full analyzer support - use for manual testing")]
    public async Task AnalyzeAsync_SkipsGeneratedFiles()
    {
        // Verify that .g.cs, .Designer.cs files are excluded
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires MSBuildWorkspace for full analyzer support - use for manual testing")]
    public async Task AnalyzeAsync_SkipsEFMigrations()
    {
        // Verify that EF Core migration files are excluded
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires MSBuildWorkspace for full analyzer support - use for manual testing")]
    public async Task AnalyzeAsync_SkipsExternalDependencies()
    {
        // Verify that NuGet package files are excluded
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires MSBuildWorkspace for full analyzer support - use for manual testing")]
    public async Task AnalyzeAsync_HandlesGlobalUsings()
    {
        // Test .NET 6+ global usings detection
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a simple in-memory solution for testing
    /// </summary>
    private static Solution CreateTestSolution(
        string projectName,
        string fileName,
        string sourceCode
    )
    {
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = new AdhocWorkspace()
            .CurrentSolution.AddProject(projectId, projectName, projectName, LanguageNames.CSharp)
            .AddMetadataReference(
                projectId,
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            )
            .AddMetadataReference(
                projectId,
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
            )
            .AddMetadataReference(
                projectId,
                MetadataReference.CreateFromFile(
                    typeof(System.Collections.Generic.List<>).Assembly.Location
                )
            )
            .AddMetadataReference(
                projectId,
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
            )
            .AddDocument(documentId, fileName, SourceText.From(sourceCode));

        return solution;
    }
}
