using System.ComponentModel;
using DotnetUnused.Core;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DotnetUnused.Mcp;

/// <summary>
/// Server-wide settings taken from the <c>dotnet-unused mcp</c> command line
/// </summary>
public sealed record McpServerSettings(string? MSBuildPath);

/// <summary>
/// MCP tools exposing the analysis pipeline. Results are structured so an agent never has to
/// parse console output, and the one tool that writes files is annotated as destructive so
/// clients ask the user before running it.
/// </summary>
[McpServerToolType]
public sealed class UnusedCodeTools
{
    private const string PathDescription =
        "Path to a .sln, .slnx or .csproj file, or to a directory containing exactly one of them. "
        + "Relative paths are resolved against the server's working directory";

    // MSBuild registration and workspace loading are not safe to run concurrently, and an
    // analysis must not overlap a fix that is rewriting the same files
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly McpServerSettings _settings;

    public UnusedCodeTools(McpServerSettings settings)
    {
        _settings = settings;
    }

    [McpServerTool(
        Name = "analyze_solution",
        Title = "Find unused code",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description(
        "Finds unused code in a C# solution or project using Roslyn: unused methods, properties, "
            + "fields and types (classes, structs, records, interfaces, enums, delegates), unused "
            + "using directives, and optionally unused NuGet packages. Read-only. Loading a large "
            + "solution can take a minute. Findings are heuristics: review unused symbols before "
            + "deleting them, since reflection, serialization or code outside the solution can use them"
    )]
    public async Task<AnalyzeSolutionResult> AnalyzeSolution(
        [Description(PathDescription)] string path,
        [Description("Report unused using directives (default true)")] bool includeUsings = true,
        [Description("Report unused NuGet packages (default false)")] bool includePackages = false,
        [Description(
            "Skip public members and types, which other assemblies may use (default true). "
                + "Set false for applications whose public API has no outside callers"
        )]
            bool excludePublic = true,
        [Description(
            "Also report virtual members, which polymorphism can call without a visible reference (default false)"
        )]
            bool includeVirtual = false,
        [Description("Never report members carrying these attributes, e.g. [\"TestInitialize\"]")]
            string[]? ignoreAttributes = null,
        [Description("Never report these NuGet package IDs as unused")]
            string[]? excludePackages = null,
        [Description(
            "Maximum entries returned per list (default 50); summary counts are always complete"
        )]
            int maxResults = 50,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (maxResults < 1)
        {
            throw new McpException("maxResults must be at least 1.");
        }

        var target = ResolveTarget(path);
        return await RunExclusiveAsync(
            async reporter =>
            {
                var started = DateTime.UtcNow;
                var loader = new SolutionLoader(_settings.MSBuildPath);
                var solution = await loader.LoadAsync(target, reporter, cancellationToken);

                var result = await SolutionAnalyzer.AnalyzeAsync(
                    solution,
                    new AnalysisOptions
                    {
                        ExcludePublic = excludePublic,
                        AnalyzeUsings = includeUsings,
                        AnalyzePackages = includePackages,
                        IncludeVirtual = includeVirtual,
                        IgnoreAttributes = ignoreAttributes ?? [],
                        ExcludePackages = excludePackages ?? [],
                    },
                    reporter,
                    cancellationToken
                );
                result.Duration = DateTime.UtcNow - started;

                return ToToolResult(
                    target,
                    result,
                    loader.LoadErrors,
                    includeUsings,
                    includePackages,
                    maxResults
                );
            },
            progress,
            cancellationToken
        );
    }

    [McpServerTool(
        Name = "fix_unused_usings",
        Title = "Remove unused using directives",
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description(
        "Removes unused using directives from source files on disk. Re-analyzes the solution first, "
            + "so it removes exactly what analyze_solution would report. Pass files to limit the change "
            + "to specific files. Run analyze_solution afterwards to verify"
    )]
    public async Task<FixUnusedUsingsResult> FixUnusedUsings(
        [Description(PathDescription)] string path,
        [Description(
            "Only fix these source files: absolute, or relative to the directory of the solution or "
                + "project, as analyze_solution reports them. Omit to fix every file"
        )]
            string[]? files = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var target = ResolveTarget(path);
        var root = Path.GetDirectoryName(target)!;
        var onlyFiles = files
            ?.Select(f => Path.GetFullPath(f, root))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return await RunExclusiveAsync(
            async reporter =>
            {
                var loader = new SolutionLoader(_settings.MSBuildPath);
                var solution = await loader.LoadAsync(target, reporter, cancellationToken);

                var unusedUsings = await new UnusedUsingAnalyzer().AnalyzeAsync(
                    solution,
                    reporter,
                    cancellationToken
                );
                var unmatchedFiles = new List<string>();
                if (onlyFiles != null)
                {
                    unusedUsings = unusedUsings
                        .Where(u => onlyFiles.Contains(Path.GetFullPath(u.FilePath)))
                        .ToList();

                    // A wrong path would otherwise look like a file with nothing to fix
                    var solutionFiles = solution
                        .Projects.SelectMany(p => p.Documents)
                        .Where(d => d.FilePath != null)
                        .Select(d => Path.GetFullPath(d.FilePath!))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    unmatchedFiles = files!
                        .Where(f => !solutionFiles.Contains(Path.GetFullPath(f, root)))
                        .ToList();
                }

                var fixer = new UsingDirectiveFixer();
                await fixer.ApplyFixesAsync(solution, unusedUsings, reporter, cancellationToken);

                var modified = fixer.ModifiedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
                return new FixUnusedUsingsResult(
                    target,
                    unusedUsings.Count(u => modified.Contains(u.FilePath)),
                    fixer
                        .ModifiedFiles.Select(f => Path.GetRelativePath(root, f))
                        .Order(StringComparer.Ordinal)
                        .ToList(),
                    unmatchedFiles,
                    loader.LoadErrors.ToList()
                );
            },
            progress,
            cancellationToken
        );
    }

    /// <summary>
    /// Maps an analysis result to the tool's structured output
    /// </summary>
    public static AnalyzeSolutionResult ToToolResult(
        string path,
        AnalysisResult result,
        IReadOnlyList<string> loadErrors,
        bool includeUsings,
        bool includePackages,
        int maxResults
    )
    {
        // File paths are relative to the solution directory: absolute paths repeated on every
        // entry would take up much of the response
        var root = Path.GetDirectoryName(path)!;

        var symbols = result
            .UnusedSymbols.Select(s => new UnusedSymbolItem(
                s.DisplayKind,
                s.Symbol.Name,
                // FullyQualifiedName omits the containing type of members; this gives
                // Sample.Helper.Run(int) rather than Run
                s.Symbol.ToDisplayString(),
                Path.GetRelativePath(root, s.FilePath),
                s.LineNumber
            ))
            .ToList();

        var usings = result
            .UnusedUsings.Select(u => new UnusedUsingItem(
                u.Namespace,
                Path.GetRelativePath(root, u.FilePath),
                u.LineNumber
            ))
            .ToList();

        var packages = result
            .UnusedPackages.Select(p => new UnusedPackageItem(
                p.PackageId,
                p.Version,
                p.ProjectName,
                Path.GetRelativePath(root, p.ProjectPath)
            ))
            .ToList();

        var summary = new AnalysisSummary(
            result.TotalSymbolsAnalyzed,
            symbols.Count,
            symbols
                .GroupBy(s => s.Kind)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count()),
            includeUsings ? usings.Count : null,
            includePackages ? result.TotalPackagesAnalyzed : null,
            includePackages ? packages.Count : null
        );

        return new AnalyzeSolutionResult(
            path,
            Math.Round(result.Duration.TotalSeconds, 2),
            loadErrors.ToList(),
            summary,
            symbols.Take(maxResults).ToList(),
            usings.Take(maxResults).ToList(),
            packages.Take(maxResults).ToList(),
            symbols.Count > maxResults || usings.Count > maxResults || packages.Count > maxResults
        );
    }

    /// <summary>
    /// Resolves the path argument to a .sln, .slnx or .csproj file. A directory resolves to
    /// its only solution file, or failing that its only project file.
    /// </summary>
    public static string ResolveTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new McpException("path is required.");
        }

        var fullPath = Path.GetFullPath(path);

        if (File.Exists(fullPath))
        {
            if (
                !fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                && !fullPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                && !fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new McpException(
                    $"Unsupported file type: {fullPath}. Expected a .sln, .slnx or .csproj file."
                );
            }
            return fullPath;
        }

        if (!Directory.Exists(fullPath))
        {
            throw new McpException($"Path not found: {fullPath}");
        }

        var solutions = Directory
            .EnumerateFiles(fullPath, "*.sln")
            .Concat(Directory.EnumerateFiles(fullPath, "*.slnx"))
            .ToList();
        var candidates =
            solutions.Count > 0
                ? solutions
                : Directory.EnumerateFiles(fullPath, "*.csproj").ToList();

        return candidates.Count switch
        {
            1 => candidates[0],
            0
                => throw new McpException(
                    $"No .sln, .slnx or .csproj file in {fullPath}. Pass the path of one directly."
                ),
            _
                => throw new McpException(
                    $"Several candidates in {fullPath}; pass one of them as path: "
                        + string.Join(", ", candidates.Select(Path.GetFileName).Order())
                ),
        };
    }

    /// <summary>
    /// Runs one tool call at a time, forwarding progress messages as MCP progress
    /// notifications and turning failures into errors the model can read
    /// </summary>
    private static async Task<T> RunExclusiveAsync<T>(
        Func<IProgress<string>, Task<T>> run,
        IProgress<ProgressNotificationValue>? progress,
        CancellationToken cancellationToken
    )
    {
        // Analyzers report from parallel loops, and progress values must increase from one
        // notification to the next, so numbering and sending happen under one lock
        var step = 0;
        var stepLock = new object();
        var reporter = new SyncProgress(message =>
        {
            lock (stepLock)
            {
                progress?.Report(
                    new ProgressNotificationValue { Progress = ++step, Message = message }
                );
            }
        });

        // A call cancelled while queued behind another must not start once the gate opens
        await Gate.WaitAsync(cancellationToken);
        try
        {
            return await run(reporter);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or McpException))
        {
            // Other exceptions reach the client as a generic failure; the message (missing file,
            // MSBuild load failure with its --msbuild-path advice, ...) is what the model needs
            throw new McpException(ex.Message, ex);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Invokes the handler on the reporting thread. <see cref="Progress{T}"/> posts to the
    /// thread pool, which can reorder messages and deliver them after the call has returned.
    /// </summary>
    private sealed class SyncProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
