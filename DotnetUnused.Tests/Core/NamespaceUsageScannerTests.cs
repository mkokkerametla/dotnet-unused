using DotnetUnused.Core;
using Xunit;

namespace DotnetUnused.Tests.Core;

public class NamespaceUsageScannerTests
{
    private readonly NamespaceUsageScanner _scanner = new();

    [Fact]
    public void ScanContent_FindsUsingStatements()
    {
        // Arrange
        var content = """
            using System;
            using System.Collections.Generic;
            using Newtonsoft.Json;

            namespace MyApp
            {
                class Program { }
            }
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Contains("System", namespaces);
        Assert.Contains("System.Collections.Generic", namespaces);
        Assert.Contains("System.Collections", namespaces);
        Assert.Contains("Newtonsoft.Json", namespaces);
        Assert.Contains("Newtonsoft", namespaces);
    }

    [Fact]
    public void ScanContent_FindsGlobalUsings()
    {
        // Arrange
        var content = """
            global using System.Linq;
            global using Microsoft.Extensions.Logging;
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Contains("System.Linq", namespaces);
        Assert.Contains("Microsoft.Extensions.Logging", namespaces);
    }

    [Fact]
    public void ScanContent_FindsUsingStatic()
    {
        // Arrange
        var content = """
            using static System.Console;
            using static System.Math;
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Contains("System.Console", namespaces);
        Assert.Contains("System.Math", namespaces);
        Assert.Contains("System", namespaces);
    }

    [Fact]
    public void ScanContent_FindsUsingAliases()
    {
        // Arrange
        var content = """
            using Json = Newtonsoft.Json;
            using JObject = Newtonsoft.Json.Linq.JObject;
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Contains("Newtonsoft.Json", namespaces);
        Assert.Contains("Newtonsoft.Json.Linq.JObject", namespaces);
    }

    [Fact]
    public void ScanContent_FindsQualifiedNames()
    {
        // Arrange
        var content = """
            class Program
            {
                void Main()
                {
                    System.Console.WriteLine("Hello");
                    var json = new Newtonsoft.Json.JsonSerializer();
                    var logger = new Microsoft.Extensions.Logging.LoggerFactory();
                }
            }
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Contains("System.Console", namespaces);
        Assert.Contains("Newtonsoft.Json.JsonSerializer", namespaces);
        Assert.Contains("Microsoft.Extensions.Logging.LoggerFactory", namespaces);
    }

    [Fact]
    public void ScanContent_FindsAttributes()
    {
        // Arrange
        var content = """
            [System.Serializable]
            [Newtonsoft.Json.JsonObject]
            class MyClass { }
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Contains("System.Serializable", namespaces);
        Assert.Contains("Newtonsoft.Json.JsonObject", namespaces);
    }

    [Fact]
    public void ScanContent_AddsPrefixNamespaces()
    {
        // Arrange
        var content = """
            using System.Text.Json.Serialization;
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Contains("System", namespaces);
        Assert.Contains("System.Text", namespaces);
        Assert.Contains("System.Text.Json", namespaces);
        Assert.Contains("System.Text.Json.Serialization", namespaces);
    }

    [Fact]
    public void ScanContent_HandlesEmptyContent()
    {
        // Arrange
        var content = "";
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Empty(namespaces);
    }

    [Fact]
    public void ScanContent_HandlesCodeWithNoNamespaces()
    {
        // Arrange
        var content = """
            class Program
            {
                static void Main()
                {
                    int x = 5;
                    string s = "hello";
                }
            }
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        // Should be empty since no namespaces are referenced
        // (simple types like int, string don't count as namespace usage)
        Assert.Empty(namespaces);
    }

    [Fact]
    public void ScanContent_HandlesMixedUsings()
    {
        // Arrange
        var content = """
            using System;
            global using System.Collections.Generic;
            using static System.Console;
            using Json = Newtonsoft.Json;

            namespace MyApp
            {
                class Program
                {
                    void Test()
                    {
                        var level = Microsoft.Extensions.Logging.LogLevel.Debug;
                    }
                }
            }
            """;
        var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content, namespaces);

        // Assert
        Assert.Contains("System", namespaces);
        Assert.Contains("System.Collections.Generic", namespaces);
        Assert.Contains("System.Console", namespaces);
        Assert.Contains("Newtonsoft.Json", namespaces);
        Assert.Contains("Microsoft.Extensions.Logging.LogLevel", namespaces);
    }

    [Fact]
    public void ScanContent_IsCaseInsensitive()
    {
        // Arrange
        var content1 = "using System.Text;";
        var content2 = "using SYSTEM.TEXT;";
        var namespaces1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namespaces2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        _scanner.ScanContent(content1, namespaces1);
        _scanner.ScanContent(content2, namespaces2);

        // Assert - both should detect System.Text (case may vary)
        Assert.Contains("System.Text", namespaces1);
        Assert.Contains("SYSTEM.TEXT", namespaces2);
    }
}
