namespace DotnetUnused.Core;

/// <summary>
/// Derives candidate namespaces from NuGet package IDs.
/// </summary>
public sealed class NamespaceDeriver
{
    /// <summary>
    /// Well-known package to namespace mappings.
    /// Many packages don't follow the convention of namespace = package name.
    /// </summary>
    private static readonly Dictionary<string, string[]> WellKnownNamespaces =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // JSON serialization
            ["Newtonsoft.Json"] = new[]
            {
                "Newtonsoft.Json",
                "Newtonsoft.Json.Linq",
                "Newtonsoft.Json.Converters"
            },
            ["System.Text.Json"] = new[] { "System.Text.Json", "System.Text.Json.Serialization" },

            // Testing frameworks
            ["xunit"] = new[] { "Xunit", "Xunit.Abstractions" },
            ["xunit.core"] = new[] { "Xunit", "Xunit.Abstractions" },
            ["xunit.assert"] = new[] { "Xunit" },
            ["NUnit"] = new[] { "NUnit.Framework" },
            ["MSTest.TestFramework"] = new[] { "Microsoft.VisualStudio.TestTools.UnitTesting" },
            ["Moq"] = new[] { "Moq", "Moq.Protected", "Moq.Language" },
            ["NSubstitute"] = new[] { "NSubstitute", "NSubstitute.Extensions" },
            ["FluentAssertions"] = new[] { "FluentAssertions", "FluentAssertions.Execution" },
            ["Shouldly"] = new[] { "Shouldly" },
            ["AutoFixture"] = new[] { "AutoFixture", "AutoFixture.Kernel" },
            ["Bogus"] = new[] { "Bogus", "Bogus.DataSets" },

            // Logging
            ["Serilog"] = new[] { "Serilog", "Serilog.Events", "Serilog.Core" },
            ["Serilog.Sinks.Console"] = new[] { "Serilog" },
            ["Serilog.Sinks.File"] = new[] { "Serilog" },
            ["NLog"] = new[] { "NLog", "NLog.Config" },
            ["log4net"] = new[] { "log4net", "log4net.Config" },

            // Dependency Injection / Mediator
            ["MediatR"] = new[] { "MediatR" },
            ["MediatR.Contracts"] = new[] { "MediatR" },
            ["Autofac"] = new[] { "Autofac", "Autofac.Core" },
            ["Ninject"] = new[] { "Ninject", "Ninject.Modules" },

            // Database / ORM
            ["Dapper"] = new[] { "Dapper" },
            ["Microsoft.EntityFrameworkCore"] = new[]
            {
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Metadata"
            },
            ["Npgsql"] = new[] { "Npgsql" },
            ["MySql.Data"] = new[] { "MySql.Data.MySqlClient" },

            // Web / HTTP
            ["RestSharp"] = new[] { "RestSharp" },
            ["Polly"] = new[] { "Polly", "Polly.Retry" },
            ["Refit"] = new[] { "Refit" },

            // Utilities
            ["AutoMapper"] = new[] { "AutoMapper" },
            ["FluentValidation"] = new[] { "FluentValidation", "FluentValidation.Results" },
            ["Humanizer"] = new[] { "Humanizer" },
            ["Humanizer.Core"] = new[] { "Humanizer" },

            // Console / UI
            ["Spectre.Console"] = new[] { "Spectre.Console", "Spectre.Console.Rendering" },
            ["CommandLineParser"] = new[] { "CommandLine", "CommandLine.Text" },

            // Microsoft Extensions
            ["Microsoft.Extensions.DependencyInjection"] = new[]
            {
                "Microsoft.Extensions.DependencyInjection"
            },
            ["Microsoft.Extensions.Logging"] = new[] { "Microsoft.Extensions.Logging" },
            ["Microsoft.Extensions.Configuration"] = new[] { "Microsoft.Extensions.Configuration" },
            ["Microsoft.Extensions.Options"] = new[] { "Microsoft.Extensions.Options" },
            ["Microsoft.Extensions.Hosting"] = new[] { "Microsoft.Extensions.Hosting" },

            // Code Analysis
            ["Microsoft.CodeAnalysis.CSharp"] = new[]
            {
                "Microsoft.CodeAnalysis",
                "Microsoft.CodeAnalysis.CSharp",
                "Microsoft.CodeAnalysis.CSharp.Syntax"
            },
            ["Microsoft.CodeAnalysis.Workspaces.MSBuild"] = new[]
            {
                "Microsoft.CodeAnalysis.MSBuild"
            },
            ["Microsoft.Build.Locator"] = new[] { "Microsoft.Build.Locator" },
        };

    /// <summary>
    /// Derives candidate namespaces for a given package ID.
    /// </summary>
    public string[] DeriveNamespaces(string packageId)
    {
        if (string.IsNullOrEmpty(packageId))
        {
            return Array.Empty<string>();
        }

        // Check well-known mappings first
        if (WellKnownNamespaces.TryGetValue(packageId, out var knownNamespaces))
        {
            return knownNamespaces;
        }

        // Apply heuristics for unknown packages
        var candidates = new List<string>();

        // Primary: use package ID as-is (most common case)
        candidates.Add(packageId);

        // Handle common prefixes that get removed in namespaces
        // e.g., "Microsoft.AspNetCore.Mvc" -> "Microsoft.AspNetCore.Mvc"
        // But also "Foo.Core" might just be "Foo"
        if (packageId.EndsWith(".Core", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(packageId[..^5]);
        }

        if (packageId.EndsWith(".Abstractions", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(packageId[..^13]);
        }

        // Handle packages like "Company.Product" that might have namespace "Product"
        var dotIndex = packageId.LastIndexOf('.');
        if (dotIndex > 0)
        {
            var lastPart = packageId[(dotIndex + 1)..];
            if (!IsCommonSuffix(lastPart))
            {
                candidates.Add(lastPart);
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Checks if a string is a common package suffix that shouldn't be used alone.
    /// </summary>
    private static bool IsCommonSuffix(string part)
    {
        var commonSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Core",
            "Abstractions",
            "Common",
            "Shared",
            "Client",
            "Server",
            "Extensions",
            "Helpers",
            "Utilities",
            "Utils",
            "Sdk",
            "Api"
        };
        return commonSuffixes.Contains(part);
    }
}
