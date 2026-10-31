using DotnetUnused.Models;
using Microsoft.CodeAnalysis;

namespace DotnetUnused.Core;

/// <summary>
/// Orchestrates the unused NuGet package detection pipeline.
/// </summary>
public sealed class UnusedPackageAnalyzer
{
    private readonly PackageReferenceExtractor _extractor = new();
    private readonly NamespaceUsageScanner _scanner = new();
    private readonly UnusedPackageDetector _detector;

    public UnusedPackageAnalyzer(
        IEnumerable<string>? excludedPackages = null,
        IProgress<string>? verbose = null
    )
    {
        _detector = new UnusedPackageDetector(excludedPackages, verbose);
    }

    /// <summary>
    /// Analyzes a solution for unused NuGet packages.
    /// </summary>
    /// <param name="solution">The solution to analyze.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="usedAssemblyNames">
    /// Assembly names behind referenced symbols (from the reference walker),
    /// used to recognize packages whose namespaces don't follow their IDs.
    /// May be null; the namespace heuristic still applies.
    /// </param>
    /// <returns>Analysis results containing unused packages and total count.</returns>
    public async Task<UnusedPackageAnalysisResult> AnalyzeAsync(
        Solution solution,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        HashSet<string>? usedAssemblyNames = null
    )
    {
        progress?.Report("Starting unused package analysis...");

        // Step 1: Extract package references from all projects
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Extracting package references...");
        var packages = await _extractor.ExtractAsync(solution, progress, cancellationToken);
        progress?.Report($"Found {packages.Count} package references");

        // Step 2: Scan source files for namespace usages
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Scanning source files for namespace usages...");
        var foundNamespaces = await _scanner.ScanAsync(solution, progress, cancellationToken);

        // Step 3: Map packages to compile assemblies from restore assets files
        var packageAssemblies = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase
        );
        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(project.FilePath))
            {
                continue;
            }

            var projectDir = Path.GetDirectoryName(project.FilePath);
            if (projectDir == null)
            {
                continue;
            }

            foreach (
                var (packageId, assemblies) in AssetsFileReader.ReadCompileAssemblies(projectDir)
            )
            {
                if (!packageAssemblies.TryGetValue(packageId, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    packageAssemblies[packageId] = set;
                }
                set.UnionWith(assemblies);
            }
        }

        // Step 4: Detect unused packages
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Detecting unused packages...");
        var unusedPackages = _detector.DetectUnused(
            packages,
            foundNamespaces,
            progress,
            packageAssemblies,
            usedAssemblyNames
        );

        progress?.Report(
            $"Unused package analysis complete: {unusedPackages.Count} potentially unused packages found"
        );

        return new UnusedPackageAnalysisResult
        {
            UnusedPackages = unusedPackages,
            TotalPackagesAnalyzed = packages.Count,
        };
    }
}

/// <summary>
/// Contains the results of unused package analysis.
/// </summary>
public sealed class UnusedPackageAnalysisResult
{
    public List<UnusedPackageInfo> UnusedPackages { get; init; } = new();
    public int TotalPackagesAnalyzed { get; init; }
}
