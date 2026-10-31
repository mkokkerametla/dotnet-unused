using DotnetUnused.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DotnetUnused.Tests.Core;

/// <summary>
/// A multi-targeted project is loaded once per target framework, so the same file is compiled
/// by several projects with different preprocessor symbols. Simulated here with in-memory
/// projects (no MSBuild); unused-using diagnostics need documentation comments in Diagnose mode.
/// </summary>
public class UnusedUsingMultiTargetTests
{
    private static readonly MetadataReference[] References = AppContext
        .GetData("TRUSTED_PLATFORM_ASSEMBLIES")!
        .ToString()!
        .Split(Path.PathSeparator)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray();

    private static Solution CreateSolution(string source, params string[][] targetSymbols)
    {
        var solution = new AdhocWorkspace().CurrentSolution;
        foreach (var symbols in targetSymbols)
        {
            var projectId = ProjectId.CreateNewId();
            solution = solution
                .AddProject(projectId, "Lib", "Lib", LanguageNames.CSharp)
                .WithProjectCompilationOptions(
                    projectId,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                )
                .WithProjectParseOptions(
                    projectId,
                    new CSharpParseOptions(
                        documentationMode: DocumentationMode.Diagnose,
                        preprocessorSymbols: symbols
                    )
                )
                .AddMetadataReferences(projectId, References)
                .AddDocument(
                    DocumentId.CreateNewId(projectId),
                    "Code.cs",
                    SourceText.From(source),
                    filePath: "/virtual/Code.cs"
                );
        }
        return solution;
    }

    [Fact]
    public async Task KeepsUsingNeededByOnlyOneTargetFramework()
    {
        var solution = CreateSolution(
            """
            using System.Text;

            public static class Formatter
            {
            #if NET10_0_OR_GREATER
                public static string Format(string s) => new StringBuilder(s).ToString();
            #else
                public static string Format(string s) => s;
            #endif
            }
            """,
            [],
            ["NET10_0_OR_GREATER"]
        );

        var unused = await new UnusedUsingAnalyzer().AnalyzeAsync(solution);

        Assert.Empty(unused);
    }

    [Fact]
    public async Task ReportsUsingUnusedInEveryTargetFrameworkOnce()
    {
        var solution = CreateSolution(
            """
            using System.Text;

            public static class Formatter
            {
                public static string Format(string s) => s;
            }
            """,
            [],
            ["NET10_0_OR_GREATER"]
        );

        var unused = await new UnusedUsingAnalyzer().AnalyzeAsync(solution);

        var directive = Assert.Single(unused);
        Assert.Equal(1, directive.LineNumber);
    }
}
