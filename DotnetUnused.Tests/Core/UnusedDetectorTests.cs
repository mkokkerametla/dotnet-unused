using System.Collections.Concurrent;
using DotnetUnused.Core;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace DotnetUnused.Tests.Core;

/// <summary>
/// Tests for <see cref="UnusedDetector"/> heuristics around ignored attributes and
/// test-framework lifecycle methods. Uses a lightweight in-memory Roslyn compilation
/// (no MSBuild) to obtain real <see cref="ISymbol"/> instances.
/// </summary>
public class UnusedDetectorTests
{
    private static ConcurrentBag<SymbolDefinition> DeclaredMethods(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var model = compilation.GetSemanticModel(tree);
        var bag = new ConcurrentBag<SymbolDefinition>();

        foreach (var methodDecl in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var symbol = model.GetDeclaredSymbol(methodDecl);
            if (symbol != null)
            {
                bag.Add(new SymbolDefinition(symbol, symbol.Locations[0]));
            }
        }

        return bag;
    }

    [Fact]
    public void DetectUnused_DoesNotFlagMethodWithIgnoredAttribute()
    {
        const string source = """
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class KeepMeAttribute : Attribute { }

            public class Sample
            {
                private void Plain() { }

                [KeepMe]
                private void Annotated() { }
            }
            """;

        var declared = DeclaredMethods(source);
        var referenced = new ConcurrentBag<ISymbol>();

        // Ignore attribute specified without the "Attribute" suffix.
        var detector = new UnusedDetector(excludePublicApi: true, ignoreAttributes: ["KeepMe"]);
        var result = detector.DetectUnused(declared, referenced);

        Assert.Contains(result.UnusedSymbols, s => s.Symbol.Name == "Plain");
        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Annotated");
    }

    [Fact]
    public void DetectUnused_FlagsAnnotatedMethod_WhenAttributeNotIgnored()
    {
        const string source = """
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class KeepMeAttribute : Attribute { }

            public class Sample
            {
                [KeepMe]
                private void Annotated() { }
            }
            """;

        var declared = DeclaredMethods(source);
        var referenced = new ConcurrentBag<ISymbol>();

        var detector = new UnusedDetector(excludePublicApi: true);
        var result = detector.DetectUnused(declared, referenced);

        Assert.Contains(result.UnusedSymbols, s => s.Symbol.Name == "Annotated");
    }

    [Theory]
    [InlineData("TestInitialize")]
    [InlineData("TestCleanup")]
    [InlineData("SetUp")]
    [InlineData("TearDown")]
    [InlineData("OneTimeSetUp")]
    public void DetectUnused_DoesNotFlagTestLifecycleMethods(string attributeName)
    {
        var source = $$"""
            using System;

            public sealed class {{attributeName}}Attribute : Attribute { }

            public class Fixtures
            {
                [{{attributeName}}]
                private void Hook() { }
            }
            """;

        var declared = DeclaredMethods(source);
        var referenced = new ConcurrentBag<ISymbol>();

        var detector = new UnusedDetector(excludePublicApi: true);
        var result = detector.DetectUnused(declared, referenced);

        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Hook");
    }
}
