using DotnetUnused.Models;
using DotnetUnused.Reporting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DotnetUnused.Tests.Reporting;

[Collection("Console")]
public class ReportWriterConsoleTests
{
    [Fact]
    public void WriteConsoleReport_WithBracketedValues_DoesNotThrow()
    {
        var result = new AnalysisResult
        {
            Duration = TimeSpan.FromSeconds(1),
            TotalSymbolsAnalyzed = 1,
            TotalReferencesFound = 0,
            TotalPackagesAnalyzed = 1
        };

        result.AddUnusedSymbol(CreateUnusedMethodSymbol("/tmp/project[1]/Class1.cs"));
        result.AddUnusedUsing(
            new UsingDirectiveInfo
            {
                FilePath = "/tmp/project[1]/Usings[1].cs",
                LineNumber = 3,
                Namespace = "System.Collections.Generic[Tests]",
                Message = "Unused"
            }
        );
        result.AddUnusedPackage(
            new UnusedPackageInfo
            {
                ProjectName = "My[Project]",
                ProjectPath = "/tmp/project[1]/My[Project].csproj",
                PackageId = "Pkg[Core]",
                Version = "1.0.[0]"
            }
        );

        var writer = new ReportWriter();
        var exception = Record.Exception(() => writer.WriteConsoleReport(result));

        Assert.Null(exception);
    }

    private static SymbolDefinition CreateUnusedMethodSymbol(string filePath)
    {
        const string source = """
            namespace Demo;

            public class Example
            {
                private void Unused() { }
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(source), path: filePath);
        var compilation = CSharpCompilation.Create(
            assemblyName: "DotnetUnused.Tests",
            syntaxTrees: [syntaxTree],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var methodDeclaration = syntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var symbol = semanticModel.GetDeclaredSymbol(methodDeclaration);

        Assert.NotNull(symbol);

        return new SymbolDefinition(symbol!, methodDeclaration.Identifier.GetLocation());
    }
}
