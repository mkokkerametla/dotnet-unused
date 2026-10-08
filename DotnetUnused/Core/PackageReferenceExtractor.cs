using System.Xml.Linq;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;

namespace DotnetUnused.Core;

/// <summary>
/// Extracts NuGet package references from project files.
/// </summary>
public sealed class PackageReferenceExtractor
{
    /// <summary>
    /// Extracts all package references from projects in a solution.
    /// </summary>
    public async Task<List<PackageReferenceInfo>> ExtractAsync(
        Solution solution,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var packages = new List<PackageReferenceInfo>();
        var centralVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // First, try to find and parse Directory.Packages.props for central package management
        var solutionDir = Path.GetDirectoryName(solution.FilePath);
        if (!string.IsNullOrEmpty(solutionDir))
        {
            var propsPath = FindDirectoryPackagesProps(solutionDir);
            if (propsPath != null)
            {
                progress?.Report(
                    $"Found central package management: {Path.GetFileName(propsPath)}"
                );
                centralVersions = await ParseCentralPackageVersionsAsync(
                    propsPath,
                    cancellationToken
                );
            }
        }

        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(project.FilePath) || !File.Exists(project.FilePath))
            {
                continue;
            }

            progress?.Report($"Extracting packages from: {project.Name}");
            var projectPackages = await ExtractFromProjectAsync(
                project.FilePath,
                project.Name,
                centralVersions,
                cancellationToken
            );
            packages.AddRange(projectPackages);
        }

        return packages;
    }

    /// <summary>
    /// Extracts package references from a single project file.
    /// </summary>
    public async Task<List<PackageReferenceInfo>> ExtractFromProjectAsync(
        string projectPath,
        string projectName,
        Dictionary<string, string>? centralVersions = null,
        CancellationToken cancellationToken = default
    )
    {
        var packages = new List<PackageReferenceInfo>();

        try
        {
            var content = await File.ReadAllTextAsync(projectPath, cancellationToken);
            var doc = XDocument.Parse(content);

            // Find all PackageReference elements
            var packageRefs = doc.Descendants().Where(e => e.Name.LocalName == "PackageReference");

            foreach (var packageRef in packageRefs)
            {
                var packageId = packageRef.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(packageId))
                {
                    continue;
                }

                // Version can be an attribute or a child element
                var version =
                    packageRef.Attribute("Version")?.Value
                    ?? packageRef
                        .Elements()
                        .FirstOrDefault(e => e.Name.LocalName == "Version")
                        ?.Value
                    ?? string.Empty;

                // If version is empty and central package management is in use, look it up
                if (string.IsNullOrEmpty(version) && centralVersions != null)
                {
                    centralVersions.TryGetValue(packageId, out version);
                    version ??= string.Empty;
                }

                packages.Add(
                    new PackageReferenceInfo
                    {
                        PackageId = packageId,
                        Version = version,
                        ProjectPath = projectPath,
                        ProjectName = projectName,
                        IncludesCompileAssets = DeterminesCompileAssets(packageRef)
                    }
                );
            }
        }
        catch (Exception)
        {
            // Skip files that cannot be parsed
        }

        return packages;
    }

    /// <summary>
    /// Determines whether a package reference contributes compile-time assets.
    /// A package that only supplies build/analyzer/runtime assets (via IncludeAssets that omits
    /// "compile", or ExcludeAssets that includes "compile") exposes no API in code, so it has no
    /// namespace to detect and must not be flagged as unused. PrivateAssets does not affect this,
    /// since it only controls transitive flow, not the current project's compilation.
    /// </summary>
    private static bool DeterminesCompileAssets(XElement packageRef)
    {
        var includeAssets = GetMetadata(packageRef, "IncludeAssets");
        var excludeAssets = GetMetadata(packageRef, "ExcludeAssets");

        // IncludeAssets, when present, is an allow-list. Compile assets are present only when it
        // contains "compile" or "all".
        if (!string.IsNullOrWhiteSpace(includeAssets))
        {
            if (!ContainsAsset(includeAssets, "compile") && !ContainsAsset(includeAssets, "all"))
            {
                return false;
            }
        }

        // ExcludeAssets removes assets. Excluding "compile" (or "all") drops compile-time assets.
        if (ContainsAsset(excludeAssets, "compile") || ContainsAsset(excludeAssets, "all"))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads MSBuild item metadata that may be expressed either as an attribute or a child element.
    /// </summary>
    private static string? GetMetadata(XElement element, string name) =>
        element.Attribute(name)?.Value
        ?? element
            .Elements()
            .FirstOrDefault(e =>
                string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)
            )
            ?.Value;

    /// <summary>
    /// Checks whether a semicolon-separated asset list contains a given token (case-insensitive).
    /// </summary>
    private static bool ContainsAsset(string? assets, string token)
    {
        if (string.IsNullOrWhiteSpace(assets))
        {
            return false;
        }

        return assets
            .Split(
                [';', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Any(a => string.Equals(a, token, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds Directory.Packages.props file walking up from the solution directory.
    /// </summary>
    private static string? FindDirectoryPackagesProps(string startDir)
    {
        var current = startDir;
        while (!string.IsNullOrEmpty(current))
        {
            var propsPath = Path.Combine(current, "Directory.Packages.props");
            if (File.Exists(propsPath))
            {
                return propsPath;
            }

            var parent = Path.GetDirectoryName(current);
            if (parent == current)
            {
                break;
            }
            current = parent;
        }
        return null;
    }

    /// <summary>
    /// Parses central package versions from Directory.Packages.props.
    /// </summary>
    private static async Task<Dictionary<string, string>> ParseCentralPackageVersionsAsync(
        string propsPath,
        CancellationToken cancellationToken
    )
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var content = await File.ReadAllTextAsync(propsPath, cancellationToken);
            var doc = XDocument.Parse(content);

            // Find PackageVersion elements (used in central package management)
            var packageVersions = doc.Descendants()
                .Where(e => e.Name.LocalName == "PackageVersion");

            foreach (var pkgVersion in packageVersions)
            {
                var packageId = pkgVersion.Attribute("Include")?.Value;
                var version = pkgVersion.Attribute("Version")?.Value;

                if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(version))
                {
                    versions[packageId] = version;
                }
            }
        }
        catch (Exception)
        {
            // Return empty dictionary on parse failure
        }

        return versions;
    }
}
