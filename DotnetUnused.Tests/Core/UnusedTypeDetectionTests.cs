using DotnetUnused.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DotnetUnused.Tests.Core;

/// <summary>
/// End-to-end tests of unused type detection: index declarations, walk references and apply
/// the detector heuristics on an in-memory solution (no MSBuild).
/// </summary>
public class UnusedTypeDetectionTests
{
    private static readonly MetadataReference[] References = AppContext
        .GetData("TRUSTED_PLATFORM_ASSEMBLIES")!
        .ToString()!
        .Split(Path.PathSeparator)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToArray();

    private static async Task<List<(string Kind, string Name)>> FindUnusedAsync(
        string source,
        int projectCopies = 1,
        bool excludePublic = false
    )
    {
        var solution = new AdhocWorkspace().CurrentSolution;
        for (var i = 0; i < projectCopies; i++)
        {
            var projectId = ProjectId.CreateNewId();
            solution = solution
                .AddProject(projectId, $"Project{i}", $"Project{i}", LanguageNames.CSharp)
                .WithProjectCompilationOptions(
                    projectId,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                )
                .AddMetadataReferences(projectId, References)
                .AddDocument(
                    DocumentId.CreateNewId(projectId),
                    "Test.cs",
                    SourceText.From(source),
                    filePath: "/virtual/Test.cs"
                );
        }

        var declared = await new SymbolIndexer().CollectDeclaredSymbolsAsync(solution);
        var referenced = await new ReferenceWalker().CollectReferencedSymbolsAsync(solution);
        var result = new UnusedDetector(excludePublic).DetectUnused(declared, referenced);

        return result.UnusedSymbols.Select(s => (s.DisplayKind, s.Symbol.Name)).ToList();
    }

    [Fact]
    public async Task ReportsUnusedTypesOfEveryKind()
    {
        var unused = await FindUnusedAsync(
            """
            internal class C { }
            internal struct S { }
            internal interface I { }
            internal enum E { A }
            internal record R(int Id);
            internal delegate void D();
            """
        );

        Assert.Contains(("Class", "C"), unused);
        Assert.Contains(("Struct", "S"), unused);
        Assert.Contains(("Interface", "I"), unused);
        Assert.Contains(("Enum", "E"), unused);
        Assert.Contains(("Record", "R"), unused);
        Assert.Contains(("Delegate", "D"), unused);
    }

    [Fact]
    public async Task DoesNotReportReferencedTypes()
    {
        var unused = await FindUnusedAsync(
            """
            using System;
            internal class Used { }
            internal class Base { }
            internal class Derived : Base { }
            internal interface IShape { }
            internal class Square : IShape { }
            internal enum Mode { A }
            internal struct Point { }
            internal delegate void Callback();
            internal class MarkerAttribute : Attribute { }
            internal static class Extensions { public static int Twice(this int x) => x * 2; }
            internal class Generic<T> { }

            internal class Entry
            {
                [Marker] private Mode _mode = Mode.A;
                private Point _p;
                private Callback? _cb;
                private Generic<Used> _g = new();
                public static void Main()
                {
                    _ = new Used();
                    Base b = new Derived();
                    IShape s = new Square();
                    _ = 2.Twice();
                    _ = (b, s);
                }
            }
            """
        );

        var types = unused.Where(u => u.Kind is not ("Field" or "Method" or "Property")).ToList();
        Assert.Empty(types);
    }

    [Fact]
    public async Task ReportsTypeThatOnlyReferencesItself()
    {
        var unused = await FindUnusedAsync(
            """
            internal class Node
            {
                public Node Clone() => new Node();
            }
            """
        );

        Assert.Contains(("Class", "Node"), unused);
    }

    [Fact]
    public async Task ReportsUnusedPartialTypeOnce()
    {
        var unused = await FindUnusedAsync(
            """
            internal partial class Split { }
            internal partial class Split { }
            """
        );

        Assert.Single(unused, u => u.Name == "Split");
    }

    [Fact]
    public async Task SkipsPublicTypesByDefault()
    {
        var unused = await FindUnusedAsync("public class Api { }", excludePublic: true);

        Assert.DoesNotContain(unused, u => u.Name == "Api");
    }

    [Fact]
    public async Task SkipsTestClassesAndFrameworkDerivedTypes()
    {
        var unused = await FindUnusedAsync(
            """
            using System;
            using System.Collections.ObjectModel;
            internal class FactAttribute : Attribute { }
            internal class Tests { [Fact] public void Works() { } }
            internal class Names : Collection<string> { }
            internal class Disposable : IDisposable { public void Dispose() { } }
            """
        );

        Assert.DoesNotContain(unused, u => u.Name == "Tests");
        Assert.DoesNotContain(unused, u => u.Name == "Names");
        // IDisposable is a plain BCL interface, so it says nothing about who creates the type
        Assert.Contains(("Class", "Disposable"), unused);
    }

    [Fact]
    public async Task SkipsTypeContainingEntryPoint()
    {
        var unused = await FindUnusedAsync(
            "internal class Program { public static void Main() { } }"
        );

        Assert.DoesNotContain(unused, u => u.Name == "Program");
    }

    [Fact]
    public async Task InterfaceImplementationIsNotReportedAsUnused()
    {
        var unused = await FindUnusedAsync(
            """
            using System;
            internal sealed class Reporter : IProgress<string>
            {
                public void Report(string value) { }
            }
            internal class Entry
            {
                public static void Main() { IProgress<string> p = new Reporter(); p.Report("x"); }
            }
            """
        );

        Assert.DoesNotContain(unused, u => u.Name == "Report");
    }

    [Fact]
    public async Task ExtensionMethodCallCountsAsUse()
    {
        var unused = await FindUnusedAsync(
            """
            internal static class Ext { public static string Shout(this string s) => s; }
            internal class Entry { public static void Main() { _ = "a".Shout(); } }
            """
        );

        Assert.DoesNotContain(unused, u => u.Name == "Shout");
        Assert.DoesNotContain(unused, u => u.Name == "Ext");
    }

    [Fact]
    public async Task FrameworkInterfaceImplementationsAreNotReportedAsUnused()
    {
        var unused = await FindUnusedAsync(
            """
            using System.Collections;
            using System.Collections.Generic;
            internal class Bag : IEnumerable<int>
            {
                public IEnumerator<int> GetEnumerator() { yield return 1; }
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
            internal class Cmp : IComparer<int>
            {
                int IComparer<int>.Compare(int a, int b) => a - b;
            }
            internal class Entry
            {
                public static void Main()
                {
                    foreach (var x in new Bag()) { }
                    new List<int>().Sort(new Cmp());
                }
            }
            """
        );

        Assert.DoesNotContain(unused, u => u.Name.EndsWith("GetEnumerator"));
        Assert.DoesNotContain(unused, u => u.Name.EndsWith("Compare"));
    }

    [Fact]
    public async Task SourceInterfaceImplementationIsReportedWhenInterfaceMemberIsUnused()
    {
        var unused = await FindUnusedAsync(
            """
            internal interface IWorker { void Work(); }
            internal class Worker : IWorker { void IWorker.Work() { } }
            internal class Entry { public static void Main() { IWorker w = new Worker(); } }
            """
        );

        Assert.Contains(unused, u => u.Name.EndsWith("Work"));
    }

    [Fact]
    public async Task ReportsUnusedCustomExceptions()
    {
        var unused = await FindUnusedAsync(
            """
            using System;
            internal class MyError : Exception { }
            internal class MyInvalidOperation : InvalidOperationException { }
            """
        );

        Assert.Contains(("Class", "MyError"), unused);
        Assert.Contains(("Class", "MyInvalidOperation"), unused);
    }

    [Fact]
    public async Task ExtensionDeconstructCountsAsUse()
    {
        var unused = await FindUnusedAsync(
            """
            internal record struct P(int X);
            internal static class Ext
            {
                internal static void Deconstruct(this P p, out int a, out int b) { a = p.X; b = 0; }
            }
            internal class Entry
            {
                public static int Main()
                {
                    var (a, b) = new P(1);
                    foreach (var (c, d) in new[] { new P(2) }) { a += c + d; }
                    return a + b;
                }
            }
            """
        );

        Assert.DoesNotContain(unused, u => u.Name == "Deconstruct");
        Assert.DoesNotContain(unused, u => u.Name == "Ext");
    }

    [Fact]
    public async Task SameDeclarationInMultipleTargetsIsReportedOnce()
    {
        var unused = await FindUnusedAsync("internal class Orphan { }", projectCopies: 2);

        Assert.Single(unused, u => u.Name == "Orphan");
    }
}
