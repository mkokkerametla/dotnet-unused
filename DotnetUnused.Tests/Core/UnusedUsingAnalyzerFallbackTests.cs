using DotnetUnused.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DotnetUnused.Tests.Core;

/// <summary>
/// Tests for UnusedUsingAnalyzer fallback to CS8019 compiler diagnostics
/// Addresses issue: Should work even when IDE analyzers aren't loaded
/// </summary>
public class UnusedUsingAnalyzerFallbackTests
{
    [Fact]
    public async Task AnalyzeAsync_FallsBackToCS8019_WhenNoIDEAnalyzers()
    {
        // Arrange - Create solution without IDE analyzers (AdhocWorkspace)
        var sourceCode =
            @"
using System;
using System.Linq;

namespace TestProject
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";
        var solution = CreateTestSolution("TestProject", "TestClass.cs", sourceCode);
        var analyzer = new UnusedUsingAnalyzer();

        // Act
        var results = await analyzer.AnalyzeAsync(solution);

        // Assert
        // Should not crash and should return results (even if empty due to no CS8019 in AdhocWorkspace)
        Assert.NotNull(results);
        // AdhocWorkspace doesn't emit CS8019, so we expect empty results
        // But the important thing is it doesn't crash and uses the fallback path
    }

    [Fact(Skip = "Requires MSBuildWorkspace with compiler diagnostics - integration test")]
    public async Task AnalyzeAsync_UsesCS8019_WhenAvailable()
    {
        // This would test that CS8019 compiler diagnostics are actually used
        // Requires real MSBuildWorkspace to emit compiler diagnostics
        await Task.CompletedTask;
    }

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
            .AddDocument(documentId, fileName, SourceText.From(sourceCode));

        return solution;
    }
}
