using DotnetUnused.Models;

namespace DotnetUnused.Core;

/// <summary>
/// Detects unused NuGet packages by comparing package namespaces to actual usage.
/// </summary>
public sealed class UnusedPackageDetector
{
    /// <summary>
    /// Known build/tooling packages that don't have direct code usage.
    /// These should always be considered "used" as they provide build-time functionality.
    /// </summary>
    private static readonly HashSet<string> KnownBuildPackages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Test infrastructure (no direct code reference)
            "Microsoft.NET.Test.Sdk",
            "coverlet.collector",
            "coverlet.msbuild",
            "xunit.runner.visualstudio",
            "NUnit3TestAdapter",
            "MSTest.TestAdapter",
            // Analyzers and code generation
            "Microsoft.CodeAnalysis.Analyzers",
            "Microsoft.CodeAnalysis.NetAnalyzers",
            "StyleCop.Analyzers",
            "SonarAnalyzer.CSharp",
            "Roslynator.Analyzers",
            "Microsoft.SourceLink.GitHub",
            "MinVer",
            // Build tools
            "Microsoft.NET.Compilers.Toolset",
            "Microsoft.Build.Tasks.Git",
            "GitVersion.MsBuild",
            "Nerdbank.GitVersioning",
            // Native/runtime dependencies
            "runtime.native.System",
            "runtime.native.System.IO.Compression",
            "Microsoft.NETCore.Platforms",
            "Microsoft.NETCore.Targets",
            // Package metadata
            "Microsoft.PackageReference.Aliases"
        };

    /// <summary>
    /// Known packages that provide implicit functionality without explicit using statements.
    /// </summary>
    private static readonly HashSet<string> ImplicitUsagePackages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // These packages are typically used through inheritance or attributes
            // that may not result in explicit namespace imports
            "Microsoft.AspNetCore.App",
            "Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation",
            "Microsoft.AspNetCore.SpaProxy",
            "Microsoft.AspNetCore.SpaServices.Extensions"
        };

    private readonly NamespaceDeriver _namespaceDeriver = new();

    /// <summary>
    /// Detects unused packages by comparing derived namespaces against found usages.
    /// </summary>
    public List<UnusedPackageInfo> DetectUnused(
        List<PackageReferenceInfo> packages,
        HashSet<string> foundNamespaces,
        IProgress<string>? progress = null
    )
    {
        var unused = new List<UnusedPackageInfo>();

        foreach (var package in packages)
        {
            // Skip packages that contribute no compile-time assets (build/analyzer/runtime only,
            // e.g. DotNet.ReproducibleBuilds). They expose no namespace, so they can never be
            // detected as "used" and would otherwise be false positives.
            if (!package.IncludesCompileAssets)
            {
                continue;
            }

            // Skip known build packages
            if (IsBuildOrToolingPackage(package.PackageId))
            {
                continue;
            }

            // Skip implicit usage packages
            if (ImplicitUsagePackages.Contains(package.PackageId))
            {
                continue;
            }

            // Derive candidate namespaces for this package
            var candidateNamespaces = _namespaceDeriver.DeriveNamespaces(package.PackageId);

            // Check if any candidate namespace is found in the codebase
            var isUsed = candidateNamespaces.Any(ns =>
                foundNamespaces.Contains(ns)
                || foundNamespaces.Any(found => IsNamespaceMatch(ns, found))
            );

            if (!isUsed)
            {
                unused.Add(
                    new UnusedPackageInfo
                    {
                        PackageId = package.PackageId,
                        Version = package.Version,
                        ProjectPath = package.ProjectPath,
                        ProjectName = package.ProjectName,
                        CandidateNamespaces = candidateNamespaces.ToList()
                    }
                );
            }
        }

        progress?.Report($"Detected {unused.Count} potentially unused packages");
        return unused;
    }

    /// <summary>
    /// Checks if a package is a known build or tooling package.
    /// </summary>
    private static bool IsBuildOrToolingPackage(string packageId)
    {
        // Exact match
        if (KnownBuildPackages.Contains(packageId))
        {
            return true;
        }

        // Pattern-based detection for common tooling packages
        var lowerPackageId = packageId.ToLowerInvariant();

        // Analyzers typically end with .Analyzers
        if (lowerPackageId.EndsWith(".analyzers"))
        {
            return true;
        }

        // Source generators
        if (
            lowerPackageId.EndsWith(".sourcegenerator")
            || lowerPackageId.EndsWith(".sourcegenerators")
            || lowerPackageId.EndsWith(".generator")
            || lowerPackageId.EndsWith(".generators")
        )
        {
            return true;
        }

        // Build props/targets packages
        if (
            lowerPackageId.EndsWith(".build")
            || lowerPackageId.EndsWith(".msbuild")
            || lowerPackageId.EndsWith(".targets")
            || lowerPackageId.EndsWith(".props")
        )
        {
            return true;
        }

        // Runtime packages (often native dependencies)
        if (lowerPackageId.StartsWith("runtime."))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if two namespaces match (one is a prefix of the other).
    /// </summary>
    private static bool IsNamespaceMatch(string candidate, string found)
    {
        // Check if candidate is a prefix of found
        if (found.StartsWith(candidate + ".", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if found is a prefix of candidate
        if (candidate.StartsWith(found + ".", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
