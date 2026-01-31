using DotnetUnused.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DotnetUnused.Tests.Core;

/// <summary>
/// Tests for edge cases in namespace extraction that could fail with naive string replacement
/// </summary>
public class UnusedUsingAnalyzerEdgeCasesTests
{
    [Theory]
    [InlineData("using MyApp.UsingHelpers;", "MyApp.UsingHelpers")]
    [InlineData("using System.Threading.Tasks;", "System.Threading.Tasks")]
    [InlineData("using static System.Math;", "System.Math")] // static keyword handled by Roslyn
    [InlineData("global using System.Collections;", "System.Collections")] // global keyword
    [InlineData("global using static System.Console;", "System.Console")] // combined global + static
    [InlineData(
        "using MyAlias = System.Collections.Generic.List<int>;",
        "System.Collections.Generic.List<int>"
    )] // alias
    public void ExtractNamespace_HandlesEdgeCases(string usingStatement, string expectedNamespace)
    {
        // Arrange
        var sourceCode =
            $@"
{usingStatement}

namespace TestProject
{{
    public class TestClass {{ }}
}}";

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        // Find the using directive
        var usingDirective = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()
            .First();

        // Act - Extract the namespace name
        var actualNamespace = usingDirective.Name?.ToString();

        // Assert - Verify it's extracted correctly (not corrupted by string replacement)
        Assert.NotNull(actualNamespace);
        Assert.Equal(expectedNamespace, actualNamespace);
        Assert.DoesNotContain(" ", actualNamespace); // No extra spaces
    }

    [Fact]
    public void NamespaceWithUsingInName_IsNotCorrupted()
    {
        // This is the specific bug case mentioned in the review
        var sourceCode =
            @"
using MyApp.UsingHelpers;

namespace TestProject
{
    public class TestClass { }
}";

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        var usingDirective = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()
            .First();

        var namespaceName = usingDirective.Name?.ToString();

        // Should be "MyApp.UsingHelpers", NOT "MyApp. Helpers"
        Assert.Equal("MyApp.UsingHelpers", namespaceName);
        Assert.DoesNotContain(" ", namespaceName);
    }
}
