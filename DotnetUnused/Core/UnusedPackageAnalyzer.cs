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
    private readonly UnusedPackageDetector _detector = new();

    /// <summary>
    /// Analyzes a solution for unused NuGet packages.
    /// </summary>
    /// <param name="solution">The solution to analyze.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis results containing unused packages and total count.</returns>
    public async Task<UnusedPackageAnalysisResult> AnalyzeAsync(
        Solution solution,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
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

        // Step 3: Detect unused packages
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Detecting unused packages...");
        var unusedPackages = _detector.DetectUnused(packages, foundNamespaces, progress);

        progress?.Report(
            $"Unused package analysis complete: {unusedPackages.Count} potentially unused packages found"
        );

        return new UnusedPackageAnalysisResult
        {
            UnusedPackages = unusedPackages,
            TotalPackagesAnalyzed = packages.Count
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
