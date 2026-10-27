using DotnetUnused.Core;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DotnetUnused.Tests.Core;

/// <summary>
/// End-to-end regression tests for the unused-detection fixes: interface
/// implementations, generic overload attribution, and implicit calls
/// (using / foreach / deconstruction). Each test builds a real in-memory
/// compilation via AdhocWorkspace and runs the production indexer, walker,
/// and detector over it — no MSBuild required.
/// </summary>
public class ReviewFixesTests
{
    private static async Task<AnalysisResult> AnalyzeAsync(
        string source,
        bool excludePublic = false,
        bool includeVirtual = false
    )
    {
        var workspace = new AdhocWorkspace();
        var project = workspace
            .AddProject("Test", LanguageNames.CSharp)
            .WithMetadataReferences([
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IDisposable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            ]);
        var document = project.AddDocument("Test.cs", source);
        var solution = document.Project.Solution;

        var declared = await new SymbolIndexer(includeVirtual).CollectDeclaredSymbolsAsync(
            solution
        );
        var referenced = await new ReferenceWalker().CollectReferencedSymbolsAsync(solution);
        return new UnusedDetector(excludePublicApi: excludePublic).DetectUnused(
            declared,
            referenced
        );
    }

    private static bool IsFlagged(AnalysisResult result, string name) =>
        result.UnusedSymbols.Any(s => s.Symbol.Name == name);

    private static bool IsFlaggedAtLine(AnalysisResult result, string name, int line) =>
        result.UnusedSymbols.Any(s => s.Symbol.Name == name && s.LineNumber == line);

    [Fact]
    public async Task ExplicitInterfaceImplementation_CalledThroughInterface_IsNotFlagged()
    {
        const string source = """
            using System;

            public class Explicit : IDisposable
            {
                void IDisposable.Dispose() { }
            }

            public static class Program
            {
                public static void Main()
                {
                    ((IDisposable)new Explicit()).Dispose();
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Dispose");
    }

    [Fact]
    public async Task ImplicitInterfaceImplementation_CalledThroughInterface_IsNotFlagged()
    {
        const string source = """
            public interface IGreeter { void Greet(); }

            public class Greeter : IGreeter
            {
                public void Greet() { }
            }

            public static class Program
            {
                public static void Main()
                {
                    IGreeter g = new Greeter();
                    g.Greet();
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Greet");
    }

    [Fact]
    public async Task InterfaceImplementation_NeverUsedThroughInterface_IsStillFlagged()
    {
        // The interface fix must not hide true positives: an implementation
        // whose interface member is never referenced anywhere is still dead.
        const string source = """
            public interface IGreeter { void Greet(); }

            public class Greeter : IGreeter
            {
                public void Greet() { }
            }

            public static class Program
            {
                public static void Main() { }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.True(IsFlagged(result, "Greet"));
    }

    [Fact]
    public async Task GenericOverload_CalledOverload_IsNotFlagged_UncalledOverload_IsFlagged()
    {
        // Regression test for overload misattribution on generic types: the
        // walker used to map every overload call to the first-declared
        // overload, flagging the called overload and sparing the uncalled one.
        const string source = """
            public class Box<T>
            {
                public void Foo(int x) { }
                public void Foo(string s) { }
            }

            public static class Program
            {
                public static void Main()
                {
                    var box = new Box<int>();
                    box.Foo("hello");
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        // Foo(string) is on line 4 and is the overload actually called.
        Assert.False(IsFlaggedAtLine(result, "Foo", 4));
        // Foo(int) is on line 3 and is never called.
        Assert.True(IsFlaggedAtLine(result, "Foo", 3));
    }

    [Fact]
    public async Task GenericOverload_ReverseDirection_MatchesCall()
    {
        const string source = """
            public class Box<T>
            {
                public void Foo(int x) { }
                public void Foo(string s) { }
            }

            public static class Program
            {
                public static void Main()
                {
                    var box = new Box<int>();
                    box.Foo(42);
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.False(IsFlaggedAtLine(result, "Foo", 3));
        Assert.True(IsFlaggedAtLine(result, "Foo", 4));
    }

    [Fact]
    public async Task UsingStatement_Dispose_IsNotFlagged()
    {
        const string source = """
            using System;

            public class Resource : IDisposable
            {
                public void Dispose() { }
            }

            public static class Program
            {
                public static void Main()
                {
                    using (var r = new Resource()) { }
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Dispose");
    }

    [Fact]
    public async Task UsingDeclaration_Dispose_IsNotFlagged()
    {
        const string source = """
            using System;

            public class Resource : IDisposable
            {
                public void Dispose() { }
            }

            public static class Program
            {
                public static void Main()
                {
                    using var r = new Resource();
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Dispose");
    }

    [Fact]
    public async Task ForEach_PatternMembers_AreNotFlagged()
    {
        const string source = """
            public class MyCollection
            {
                public MyEnumerator GetEnumerator() => new MyEnumerator();
            }

            public class MyEnumerator
            {
                public bool MoveNext() => false;
                public int Current => 0;
            }

            public static class Program
            {
                public static void Main()
                {
                    foreach (var x in new MyCollection()) { }
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "GetEnumerator");
        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "MoveNext");
        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Current");
    }

    [Fact]
    public async Task Deconstruction_Deconstruct_IsNotFlagged()
    {
        const string source = """
            public class Point
            {
                public void Deconstruct(out int x, out int y) { x = 0; y = 0; }
            }

            public static class Program
            {
                public static void Main()
                {
                    var (a, b) = new Point();
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Deconstruct");
    }

    [Fact]
    public async Task DeconstructionAssignment_Deconstruct_IsNotFlagged()
    {
        const string source = """
            public class Point
            {
                public void Deconstruct(out int x, out int y) { x = 0; y = 0; }
            }

            public static class Program
            {
                public static void Main()
                {
                    int a;
                    int b;
                    (a, b) = new Point();
                }
            }
            """;

        var result = await AnalyzeAsync(source);

        Assert.DoesNotContain(result.UnusedSymbols, s => s.Symbol.Name == "Deconstruct");
    }

    [Fact]
    public async Task VirtualMethod_IsSkippedByDefault_AndFlaggedWhenOptedIn()
    {
        const string source = """
            public class Worker
            {
                protected virtual void UnusedVirtual() { }
            }

            public static class Program
            {
                public static void Main() { }
            }
            """;

        var byDefault = await AnalyzeAsync(source);
        Assert.DoesNotContain(byDefault.UnusedSymbols, s => s.Symbol.Name == "UnusedVirtual");

        var optedIn = await AnalyzeAsync(source, includeVirtual: true);
        Assert.True(IsFlagged(optedIn, "UnusedVirtual"));
    }

    [Fact]
    public void PackageDetector_AssemblyEvidence_MarksPackageUsed()
    {
        var packages = new List<PackageReferenceInfo>
        {
            new()
            {
                PackageId = "Acme.Widgets",
                Version = "1.0.0",
                ProjectName = "Test",
                ProjectPath = "Test.csproj",
            },
        };
        // No namespace in the codebase matches: the namespace heuristic alone
        // would flag this package.
        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packageAssemblies = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            ["Acme.Widgets"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Acme.Widgets",
            },
        };
        var usedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Acme.Widgets",
        };

        var unused = new UnusedPackageDetector().DetectUnused(
            packages,
            foundNamespaces,
            progress: null,
            packageAssemblies: packageAssemblies,
            usedAssemblyNames: usedAssemblyNames
        );

        Assert.Empty(unused);
    }

    [Fact]
    public void AssetsFileReader_MapsPackagesToCompileAssemblies()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "obj"));
            File.WriteAllText(
                Path.Combine(dir, "obj", "project.assets.json"),
                """
                {
                  "version": 3,
                  "targets": {
                    "net10.0": {
                      "Acme.Widgets/1.0.0": {
                        "type": "package",
                        "compile": {
                          "lib/netstandard2.0/Acme.Widgets.dll": {}
                        }
                      },
                      "Other/2.0.0": {
                        "type": "package",
                        "compile": {
                          "lib/net10.0/Other.dll": {},
                          "lib/net10.0/Other.Extra.dll": {}
                        }
                      }
                    }
                  }
                }
                """
            );

            var map = AssetsFileReader.ReadCompileAssemblies(dir);

            Assert.Equal(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Acme.Widgets" },
                map["Acme.Widgets"]
            );
            Assert.Equal(2, map["Other"].Count);
            Assert.Contains("Other", map["Other"]);
            Assert.Contains("Other.Extra", map["Other"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AssetsFileReader_MissingFile_ReturnsEmptyMap()
    {
        var map = AssetsFileReader.ReadCompileAssemblies(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        );

        Assert.Empty(map);
    }
}
