using System.Diagnostics;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;

namespace DotnetUnused.Core;

/// <summary>
/// Options for a full unused-code analysis of a loaded solution
/// </summary>
public sealed class AnalysisOptions
{
    public bool ExcludePublic { get; init; } = true;
    public bool AnalyzeUsings { get; init; } = true;
    public bool AnalyzePackages { get; init; }
    public bool IncludeVirtual { get; init; }
    public IReadOnlyCollection<string> IgnoreAttributes { get; init; } = [];
    public IReadOnlyCollection<string> ExcludePackages { get; init; } = [];

    /// <summary>
    /// Receives the per-package used/unused/skipped explanations (--verbose)
    /// </summary>
    public IProgress<string>? PackageDiagnostics { get; init; }
}

/// <summary>
/// Runs the analysis pipeline (symbols, usings, packages) over a loaded solution.
/// Shared by the CLI and the MCP server so both report the same findings.
/// </summary>
public static class SolutionAnalyzer
{
    public static async Task<AnalysisResult> AnalyzeAsync(
        Solution solution,
        AnalysisOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        // Index declared symbols
        cancellationToken.ThrowIfCancellationRequested();
        var indexer = new SymbolIndexer(options.IncludeVirtual);
        var declaredSymbols = await indexer.CollectDeclaredSymbolsAsync(
            solution,
            progress,
            cancellationToken
        );

        // Find referenced symbols
        cancellationToken.ThrowIfCancellationRequested();
        var walker = new ReferenceWalker();
        var referencedSymbols = await walker.CollectReferencedSymbolsAsync(
            solution,
            progress,
            cancellationToken
        );

        // Detect unused symbols
        cancellationToken.ThrowIfCancellationRequested();
        var detector = new UnusedDetector(options.ExcludePublic, options.IgnoreAttributes);
        var result = detector.DetectUnused(declaredSymbols, referencedSymbols, progress);

        // Analyze unused usings
        if (options.AnalyzeUsings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var usingAnalyzer = new UnusedUsingAnalyzer();
            var unusedUsings = await usingAnalyzer.AnalyzeAsync(
                solution,
                progress,
                cancellationToken
            );

            foreach (var unusedUsing in unusedUsings)
            {
                result.AddUnusedUsing(unusedUsing);
            }
        }

        // Analyze unused packages
        if (options.AnalyzePackages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var packageAnalyzer = new UnusedPackageAnalyzer(
                options.ExcludePackages,
                options.PackageDiagnostics
            );
            // Assemblies behind referenced symbols let the package detector
            // recognize a package even when its namespaces don't follow its
            // package ID (the namespace-guessing heuristic alone misses those).
            var usedAssemblyNames = new HashSet<string>(
                referencedSymbols.Select(s => s.ContainingAssembly?.Name).Where(n => n != null)!,
                StringComparer.OrdinalIgnoreCase
            );
            var packageResults = await packageAnalyzer.AnalyzeAsync(
                solution,
                progress,
                cancellationToken,
                usedAssemblyNames
            );

            result.TotalPackagesAnalyzed = packageResults.TotalPackagesAnalyzed;
            foreach (var pkg in packageResults.UnusedPackages)
            {
                result.AddUnusedPackage(pkg);
            }
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }
}
