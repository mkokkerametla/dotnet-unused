using System.Collections.Concurrent;
using System.Collections.Immutable;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotnetUnused.Core;

/// <summary>
/// Analyzes solutions for unused using directives using Roslyn's IDE0005 analyzer
/// </summary>
public sealed class UnusedUsingAnalyzer
{
    /// <summary>
    /// Analyzes the solution for unused using directives
    /// </summary>
    public async Task<List<UsingDirectiveInfo>> AnalyzeAsync(
        Solution solution,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var results = new ConcurrentBag<UsingDirectiveInfo>();
        var projects = solution.Projects.ToList();

        progress?.Report($"Analyzing unused usings in {projects.Count} projects...");

        // Process projects in parallel
        await Parallel.ForEachAsync(
            projects,
            cancellationToken,
            async (project, ct) =>
            {
                if (!project.SupportsCompilation)
                {
                    return;
                }

                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null)
                {
                    return;
                }

                try
                {
                    IEnumerable<Diagnostic> unusedUsingDiagnostics;

                    // Try to use IDE analyzers (IDE0005) if available
                    var analyzerReferences = project.AnalyzerReferences;
                    var analyzers = analyzerReferences
                        .SelectMany(r => r.GetAnalyzers(project.Language))
                        .Where(a => a != null)
                        .ToImmutableArray();

                    if (!analyzers.IsEmpty)
                    {
                        // Use IDE analyzers (preferred - more accurate)
                        var compilationWithAnalyzers = compilation.WithAnalyzers(
                            analyzers,
                            options: null
                        );
                        var allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(
                            ct
                        );

                        unusedUsingDiagnostics = allDiagnostics.Where(d =>
                            (d.Id == "CS8019" || d.Id == "IDE0005")
                            && d.Location.IsInSource
                            && d.Location.SourceTree != null
                        );
                    }
                    else
                    {
                        // Fallback: Use compiler diagnostics (CS8019) if IDE analyzers not available
                        progress?.Report(
                            $"IDE analyzers not found for {project.Name}, using compiler diagnostics (CS8019)"
                        );

                        var compilerDiagnostics = compilation.GetDiagnostics(ct);

                        unusedUsingDiagnostics = compilerDiagnostics.Where(d =>
                            d.Id == "CS8019"
                            && d.Location.IsInSource
                            && d.Location.SourceTree != null
                        );
                    }

                    foreach (var diagnostic in unusedUsingDiagnostics)
                    {
                        var lineSpan = diagnostic.Location.GetLineSpan();
                        var filePath = lineSpan.Path;

                        // Skip external dependencies (NuGet packages, SDK files)
                        if (IsExternalDependency(filePath))
                        {
                            continue;
                        }

                        // Apply FileFilter to skip generated files
                        if (!FileFilter.ShouldAnalyze(filePath))
                        {
                            continue;
                        }

                        results.Add(
                            new UsingDirectiveInfo
                            {
                                FilePath = filePath,
                                LineNumber = lineSpan.StartLinePosition.Line + 1,
                                Namespace = ExtractNamespaceFromDiagnostic(diagnostic),
                                Message = diagnostic.GetMessage()
                            }
                        );
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail the entire analysis
                    progress?.Report(
                        $"Warning: Failed to analyze usings in {project.Name}: {ex.Message}"
                    );
                }
            }
        );

        progress?.Report($"Found {results.Count} unused using directives");
        return results.OrderBy(u => u.FilePath).ThenBy(u => u.LineNumber).ToList();
    }

    /// <summary>
    /// Checks if a file path belongs to external dependencies (NuGet, SDK)
    /// Uses precise path checks to avoid false positives
    /// </summary>
    private static bool IsExternalDependency(string filePath)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');

            // Check for NuGet global packages folder (e.g., C:\Users\user\.nuget\packages\)
            if (normalizedPath.Contains("/.nuget/packages/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check for NuGet packages folder in solution (e.g., MySolution\packages\)
            if (
                normalizedPath.Contains("/packages/")
                && (
                    normalizedPath.Contains("/packages/Microsoft.")
                    || normalizedPath.Contains("/packages/System.")
                    || normalizedPath.Contains("/packages/NuGet.")
                )
            )
            {
                return true;
            }

            // Check for .NET SDK reference assemblies
            if (
                normalizedPath.Contains("/Microsoft.NET.Sdk/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Contains("/dotnet/shared/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Contains("/dotnet/packs/", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }

            // Check for Program Files .NET installations
            if (
                normalizedPath.Contains(
                    "/Program Files/dotnet/",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return true;
            }

            return false;
        }
        catch
        {
            // If path normalization fails, be conservative and don't exclude
            return false;
        }
    }

    /// <summary>
    /// Extracts the namespace from the IDE0005 diagnostic message using Roslyn syntax parsing
    /// </summary>
    private static string ExtractNamespaceFromDiagnostic(Diagnostic diagnostic)
    {
        // IDE0005 message format: "Using directive is unnecessary."
        // Use Roslyn to parse the using directive properly
        var sourceTree = diagnostic.Location.SourceTree;
        if (sourceTree != null)
        {
            try
            {
                var root = sourceTree.GetRoot();
                var node = root.FindNode(diagnostic.Location.SourceSpan);

                // Find the UsingDirectiveSyntax node
                var usingDirective =
                    node as Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax
                    ?? node.AncestorsAndSelf()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()
                        .FirstOrDefault();

                if (usingDirective != null)
                {
                    // Extract the name from the using directive
                    // This handles regular usings, static usings, and alias usings correctly
                    if (usingDirective.Alias != null)
                    {
                        // Handle: using Alias = Namespace.Type;
                        return $"{usingDirective.Alias.Name} = {usingDirective.Name}";
                    }
                    else if (
                        usingDirective.StaticKeyword.IsKind(
                            Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword
                        )
                    )
                    {
                        // Handle: using static Namespace.Type;
                        return $"static {usingDirective.Name}";
                    }
                    else
                    {
                        // Handle: using Namespace; or global using Namespace;
                        return usingDirective.Name?.ToString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // Fallback to text extraction if parsing fails
            }
        }

        return diagnostic.GetMessage();
    }
}
