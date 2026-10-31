using DotnetUnused.Mcp;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol;

namespace DotnetUnused.Tests.Mcp;

public class UnusedCodeToolsTests : IDisposable
{
    private readonly string _dir = Directory
        .CreateDirectory(
            Path.Combine(Path.GetTempPath(), "dotnet-unused-" + Guid.NewGuid().ToString("N"))
        )
        .FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void ResolveTarget_AcceptsProjectFile()
    {
        var project = Touch("App.csproj");

        Assert.Equal(project, UnusedCodeTools.ResolveTarget(project));
    }

    [Fact]
    public void ResolveTarget_PrefersSolutionInDirectory()
    {
        Touch("App.csproj");
        var solution = Touch("App.slnx");

        Assert.Equal(solution, UnusedCodeTools.ResolveTarget(_dir));
    }

    [Fact]
    public void ResolveTarget_FallsBackToOnlyProjectInDirectory()
    {
        var project = Touch("App.csproj");

        Assert.Equal(project, UnusedCodeTools.ResolveTarget(_dir));
    }

    [Fact]
    public void ResolveTarget_ListsCandidatesWhenAmbiguous()
    {
        Touch("A.sln");
        Touch("B.sln");

        var ex = Assert.Throws<McpException>(() => UnusedCodeTools.ResolveTarget(_dir));
        Assert.Contains("A.sln, B.sln", ex.Message);
    }

    [Fact]
    public void ResolveTarget_RejectsUnsupportedFiles()
    {
        var file = Touch("Program.cs");

        Assert.Throws<McpException>(() => UnusedCodeTools.ResolveTarget(file));
    }

    [Fact]
    public void ResolveTarget_RejectsEmptyDirectory()
    {
        var ex = Assert.Throws<McpException>(() => UnusedCodeTools.ResolveTarget(_dir));
        Assert.Contains("No .sln, .slnx or .csproj", ex.Message);
    }

    private static AnalysisResult ResultFor(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "/src/Code.cs");
        var model = CSharpCompilation.Create("Test", [tree]).GetSemanticModel(tree);

        var result = new AnalysisResult { TotalSymbolsAnalyzed = 10 };
        foreach (
            var node in tree.GetRoot()
                .DescendantNodes()
                .Where(n =>
                    n
                        is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax
                            or Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax
                )
        )
        {
            var symbol = model.GetDeclaredSymbol(node)!;
            result.AddUnusedSymbol(new SymbolDefinition(symbol, symbol.Locations[0]));
        }
        return result;
    }

    [Fact]
    public void ToToolResult_SummarizesByKindAndQualifiesMemberNames()
    {
        var result = ResultFor("namespace N { class A { void Run(int x) { } void Stop() { } } }");

        var tool = UnusedCodeTools.ToToolResult("/src/App.sln", result, [], true, false, 200);

        Assert.Equal(3, tool.Summary.UnusedSymbols);
        Assert.Equal(1, tool.Summary.UnusedSymbolsByKind["Class"]);
        Assert.Equal(2, tool.Summary.UnusedSymbolsByKind["Method"]);
        Assert.Contains(tool.UnusedSymbols, s => s.FullyQualifiedName == "N.A.Run(int)");
        Assert.All(tool.UnusedSymbols, s => Assert.Equal("Code.cs", s.File));
    }

    [Fact]
    public void ToToolResult_ReportsPathsRelativeToSolutionDirectory()
    {
        var result = new AnalysisResult();
        result.AddUnusedUsing(
            new UsingDirectiveInfo
            {
                FilePath = "/repo/src/App/Program.cs",
                LineNumber = 1,
                Namespace = "System.Text",
            }
        );
        result.AddUnusedUsing(
            new UsingDirectiveInfo
            {
                FilePath = "/repo/shared/Linked.cs",
                LineNumber = 2,
                Namespace = "System.Linq",
            }
        );

        var tool = UnusedCodeTools.ToToolResult("/repo/src/App.sln", result, [], true, false, 50);

        Assert.Equal(
            [Path.Combine("App", "Program.cs"), Path.Combine("..", "shared", "Linked.cs")],
            tool.UnusedUsings.Select(u => u.File)
        );
    }

    [Fact]
    public void ToToolResult_TruncatesListsButKeepsFullCounts()
    {
        var result = ResultFor("class A { void B() { } void C() { } }");

        var tool = UnusedCodeTools.ToToolResult("/src/App.sln", result, [], true, false, 1);

        Assert.True(tool.Truncated);
        Assert.Single(tool.UnusedSymbols);
        Assert.Equal(3, tool.Summary.UnusedSymbols);
    }

    [Fact]
    public void ToToolResult_LeavesCountsOfSkippedAnalysesNull()
    {
        var tool = UnusedCodeTools.ToToolResult(
            "/src/App.sln",
            new AnalysisResult(),
            ["Project X failed to load"],
            includeUsings: false,
            includePackages: false,
            maxResults: 200
        );

        Assert.Null(tool.Summary.UnusedUsings);
        Assert.Null(tool.Summary.UnusedPackages);
        Assert.Null(tool.Summary.PackagesAnalyzed);
        Assert.Equal(["Project X failed to load"], tool.LoadErrors);
        Assert.False(tool.Truncated);
    }
}
