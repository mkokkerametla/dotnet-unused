using System.Diagnostics;
using DotnetUnused.Core;
using DotnetUnused.Reporting;
using Spectre.Console;

namespace DotnetUnused;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            AnsiConsole.MarkupLine("[yellow]Cancellation requested. Cleaning up...[/]");
        };

        try
        {
            // Parse command line arguments
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return 0;
            }

            var path = args[0];
            var format = "text";
            string? outputPath = null;
            var excludePublic = true;
            var skipUsings = false;
            var fixUsings = false;
            var unusedPackages = false;
            var failOnUnused = false;
            var strict = false;
            var includeVirtual = false;
            var ignoreAttributes = new List<string>();
            string? msbuildPath = null;

            // Parse options. Unknown flags and missing/invalid values are errors:
            // silently ignoring them used to produce confusing runs (e.g. a typo
            // in --format quietly falling back to text output).
            for (int i = 1; i < args.Length; i++)
            {
                string Value(string option)
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException($"Missing value for {option}.");
                    }
                    return args[++i];
                }

                switch (args[i])
                {
                    case "--format" or "-f":
                        format = Value(args[i]);
                        break;
                    case "--output" or "-o":
                        outputPath = Value(args[i]);
                        break;
                    case "--exclude-public":
                        if (!bool.TryParse(Value(args[i]), out excludePublic))
                        {
                            AnsiConsole.MarkupLine(
                                "[red]Invalid value for --exclude-public. Expected 'true' or 'false'.[/]"
                            );
                            return 1;
                        }
                        break;
                    case "--skip-usings":
                        skipUsings = true;
                        break;
                    case "--fix":
                        fixUsings = true;
                        break;
                    case "--unused-packages":
                        unusedPackages = true;
                        break;
                    case "--fail-on-unused":
                        failOnUnused = true;
                        break;
                    case "--strict":
                        strict = true;
                        break;
                    case "--include-virtual":
                        includeVirtual = true;
                        break;
                    case "--msbuild-path":
                        msbuildPath = Value(args[i]);
                        break;
                    case "--ignore-attributes":
                        ignoreAttributes.AddRange(
                            Value(args[i])
                                .Split(
                                    ',',
                                    StringSplitOptions.RemoveEmptyEntries
                                        | StringSplitOptions.TrimEntries
                                )
                        );
                        break;
                    default:
                        AnsiConsole.MarkupLine(
                            $"[red]Unknown argument: {Markup.Escape(args[i])}. Run with --help to see supported options.[/]"
                        );
                        return 1;
                }
            }

            if (format.ToLowerInvariant() is not ("text" or "json" or "markdown" or "md"))
            {
                AnsiConsole.MarkupLine(
                    $"[red]Invalid value for --format: '{Markup.Escape(format)}'. Expected 'text', 'json', or 'markdown'.[/]"
                );
                return 1;
            }

            if (outputPath != null && format.ToLowerInvariant() == "text")
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Warning: --output is only used with --format json or markdown; ignoring it for text output.[/]"
                );
            }

            var result = await ScanAsync(
                path,
                format,
                outputPath,
                excludePublic,
                skipUsings,
                fixUsings,
                unusedPackages,
                ignoreAttributes,
                msbuildPath,
                strict,
                includeVirtual,
                cts.Token
            );

            // CI gating: by default the tool always exits 0 on a successful
            // analysis, even when findings exist. --fail-on-unused opts into a
            // non-zero exit code so a build can fail on unused code. Exit code 2
            // (distinct from 1 = error) means "analysis succeeded, findings found".
            if (
                failOnUnused
                && (
                    result.UnusedSymbols.Count > 0
                    || result.UnusedUsings.Count > 0
                    || result.UnusedPackages.Count > 0
                )
            )
            {
                AnsiConsole.MarkupLine(
                    "[red]Failing with exit code 2: unused code found (--fail-on-unused).[/]"
                );
                return 2;
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled by user.[/]");
            return 130; // Standard exit code for SIGINT
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void ShowHelp()
    {
        AnsiConsole.Write(new FigletText("Dotnet Unused").Color(Color.Cyan1));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]Detect unused code in .NET solutions[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Usage:[/]");
        AnsiConsole.MarkupLine("  dotnet-unused [[path]] [[options]]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Arguments:[/]");
        AnsiConsole.MarkupLine(
            "  [cyan]path[/]               Path to .sln, .slnx, or .csproj file"
        );
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Options:[/]");
        AnsiConsole.MarkupLine(
            "  [cyan]--format, -f[/]       Output format: text, json, or markdown (default: text)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--output, -o[/]       Output file path (for JSON or markdown format)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--exclude-public[/]   Exclude public members (default: true)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--skip-usings[/]      Skip unused using directives analysis (default: false)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--fix[/]              Automatically remove unused usings (default: false)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--unused-packages[/]  Detect unused NuGet packages (default: false)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--fail-on-unused[/]  Exit with code 2 when any unused code is found (for CI gating; default: exit 0)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--strict[/]          Treat project load errors as fatal instead of analyzing a partially loaded solution (default: false)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--include-virtual[/]  Also report unused virtual members (default: false; virtuals are skipped because polymorphism can hide their uses)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--ignore-attributes[/]  Never flag members with these attributes (comma-separated)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--msbuild-path[/]     Directory of the MSBuild to use, e.g. a .NET SDK folder (or set DOTNET_UNUSED_MSBUILD_PATH)"
        );
        AnsiConsole.MarkupLine("  [cyan]--help, -h[/]         Show help information");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Examples:[/]");
        AnsiConsole.MarkupLine("  dotnet-unused MySolution.sln");
        AnsiConsole.MarkupLine(
            "  dotnet-unused MyProject.csproj --format json --output report.json"
        );
        AnsiConsole.MarkupLine("  dotnet-unused MySolution.sln --exclude-public false");
        AnsiConsole.MarkupLine("  dotnet-unused MySolution.sln --skip-usings");
        AnsiConsole.MarkupLine("  dotnet-unused MySolution.sln --fix");
        AnsiConsole.MarkupLine("  dotnet-unused MySolution.sln --unused-packages");
        AnsiConsole.MarkupLine(
            "  dotnet-unused MySolution.slnx --format markdown --output report.md"
        );
        AnsiConsole.MarkupLine(
            "  dotnet-unused MySolution.sln --ignore-attributes TestInitialize,SetUp"
        );
    }

    private static async Task<DotnetUnused.Models.AnalysisResult> ScanAsync(
        string path,
        string format,
        string? outputPath,
        bool excludePublic,
        bool skipUsings,
        bool fixUsings,
        bool unusedPackages,
        List<string> ignoreAttributes,
        string? msbuildPath,
        bool strict,
        bool includeVirtual,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        AnsiConsole.Write(new FigletText("Dotnet Unused").Color(Color.Cyan1));
        AnsiConsole.WriteLine();

        IProgress<string> progress = new Progress<string>(msg =>
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(msg)}[/]")
        );

        // Load solution
        cancellationToken.ThrowIfCancellationRequested();
        var loader = new SolutionLoader(msbuildPath);
        var solution = await loader.LoadAsync(path, progress, cancellationToken);

        // A partially loaded solution produces silently incomplete results
        // (missing projects look like unused code, and vice versa). Surface
        // load errors prominently; --strict makes them fatal.
        if (loader.LoadErrors.Count > 0)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning: {loader.LoadErrors.Count} project load error(s) — results may be incomplete:[/]"
            );
            foreach (var loadError in loader.LoadErrors)
            {
                AnsiConsole.MarkupLine($"[yellow]  - {Markup.Escape(loadError)}[/]");
            }
            if (strict)
            {
                throw new InvalidOperationException(
                    "Aborting due to project load errors (--strict)."
                );
            }
        }

        // Index declared symbols
        cancellationToken.ThrowIfCancellationRequested();
        var indexer = new SymbolIndexer(includeVirtual);
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
        var detector = new UnusedDetector(excludePublic, ignoreAttributes);
        var result = detector.DetectUnused(declaredSymbols, referencedSymbols, progress);

        // Analyze unused usings (default behavior unless --skip-usings)
        if (!skipUsings)
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

            // Apply fixes if requested
            if (fixUsings && result.UnusedUsings.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Removing {result.UnusedUsings.Count} unused usings...");

                var fixer = new UsingDirectiveFixer();
                var filesModified = await fixer.ApplyFixesAsync(
                    solution,
                    result.UnusedUsings,
                    progress,
                    cancellationToken
                );

                if (filesModified > 0)
                {
                    AnsiConsole.MarkupLine($"[green]✓ Modified {filesModified} files[/]");
                }
            }
        }

        // Analyze unused packages (if --unused-packages flag is set)
        if (unusedPackages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var packageAnalyzer = new UnusedPackageAnalyzer();
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

        // Output results
        var reporter = new ReportWriter();

        switch (format.ToLowerInvariant())
        {
            case "json":
                var jsonPath = outputPath ?? "unused-code-report.json";
                await reporter.WriteJsonReportAsync(result, jsonPath);
                break;
            case "markdown" or "md":
                var mdPath = outputPath ?? "unused-code-report.md";
                await reporter.WriteMarkdownReportAsync(result, mdPath);
                break;
            default:
                reporter.WriteConsoleReport(result);
                break;
        }

        return result;
    }
}
