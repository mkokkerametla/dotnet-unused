using DotnetUnused.Core;
using Xunit;

namespace DotnetUnused.Tests.Core;

public class NamespaceDeriverTests
{
    private readonly NamespaceDeriver _deriver = new();

    [Theory]
    [InlineData(
        "Newtonsoft.Json",
        new[] { "Newtonsoft.Json", "Newtonsoft.Json.Linq", "Newtonsoft.Json.Converters" }
    )]
    [InlineData("xunit", new[] { "Xunit", "Xunit.Abstractions" })]
    [InlineData("Moq", new[] { "Moq", "Moq.Protected", "Moq.Language" })]
    [InlineData("FluentAssertions", new[] { "FluentAssertions", "FluentAssertions.Execution" })]
    [InlineData("Serilog", new[] { "Serilog", "Serilog.Events", "Serilog.Core" })]
    [InlineData("MediatR", new[] { "MediatR" })]
    [InlineData("Dapper", new[] { "Dapper" })]
    public void DeriveNamespaces_ReturnsWellKnownNamespaces(string packageId, string[] expected)
    {
        // Act
        var namespaces = _deriver.DeriveNamespaces(packageId);

        // Assert
        Assert.Equal(expected.OrderBy(x => x), namespaces.OrderBy(x => x));
    }

    [Fact]
    public void DeriveNamespaces_ReturnsPackageIdForUnknown()
    {
        // Arrange
        var packageId = "MyCompany.MyLibrary";

        // Act
        var namespaces = _deriver.DeriveNamespaces(packageId);

        // Assert
        Assert.Contains("MyCompany.MyLibrary", namespaces);
    }

    [Fact]
    public void DeriveNamespaces_HandlesCorePackageSuffix()
    {
        // Arrange
        var packageId = "MyPackage.Core";

        // Act
        var namespaces = _deriver.DeriveNamespaces(packageId);

        // Assert
        Assert.Contains("MyPackage.Core", namespaces);
        Assert.Contains("MyPackage", namespaces);
    }

    [Fact]
    public void DeriveNamespaces_HandlesAbstractionsSuffix()
    {
        // Arrange
        var packageId = "MyPackage.Abstractions";

        // Act
        var namespaces = _deriver.DeriveNamespaces(packageId);

        // Assert
        Assert.Contains("MyPackage.Abstractions", namespaces);
        Assert.Contains("MyPackage", namespaces);
    }

    [Fact]
    public void DeriveNamespaces_ReturnsEmptyForEmptyInput()
    {
        // Act
        var result1 = _deriver.DeriveNamespaces("");
        var result2 = _deriver.DeriveNamespaces(null!);

        // Assert
        Assert.Empty(result1);
        Assert.Empty(result2);
    }

    [Fact]
    public void DeriveNamespaces_ExtractsLastPartForCompoundNames()
    {
        // Arrange - a package like Company.Product where Product is not a common suffix
        var packageId = "Contoso.SpecialFramework";

        // Act
        var namespaces = _deriver.DeriveNamespaces(packageId);

        // Assert
        Assert.Contains("Contoso.SpecialFramework", namespaces);
        Assert.Contains("SpecialFramework", namespaces);
    }

    [Fact]
    public void DeriveNamespaces_DoesNotExtractCommonSuffixes()
    {
        // Arrange - packages ending with common suffixes shouldn't have those extracted alone
        var packageId = "MyCompany.Client";

        // Act
        var namespaces = _deriver.DeriveNamespaces(packageId);

        // Assert
        Assert.Contains("MyCompany.Client", namespaces);
        Assert.DoesNotContain("Client", namespaces); // Common suffix, shouldn't be standalone
    }

    [Theory]
    [InlineData("Microsoft.Extensions.DependencyInjection")]
    [InlineData("Microsoft.Extensions.Logging")]
    [InlineData("Microsoft.CodeAnalysis.CSharp")]
    public void DeriveNamespaces_HandlesWellKnownMicrosoftPackages(string packageId)
    {
        // Act
        var namespaces = _deriver.DeriveNamespaces(packageId);

        // Assert
        Assert.NotEmpty(namespaces);
        Assert.Contains(packageId, namespaces);
    }

    [Fact]
    public void DeriveNamespaces_IsCaseInsensitiveForWellKnown()
    {
        // Act
        var lower = _deriver.DeriveNamespaces("newtonsoft.json");
        var upper = _deriver.DeriveNamespaces("NEWTONSOFT.JSON");
        var mixed = _deriver.DeriveNamespaces("Newtonsoft.Json");

        // Assert - all should return the same well-known mappings
        Assert.Equal(lower.OrderBy(x => x), upper.OrderBy(x => x));
        Assert.Equal(upper.OrderBy(x => x), mixed.OrderBy(x => x));
    }
}
