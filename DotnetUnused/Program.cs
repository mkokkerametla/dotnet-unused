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
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
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

            // Parse options
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--format"
                    or "-f":
                        if (i + 1 < args.Length)
                        {
                            format = args[++i];
                        }
                        break;
                    case "--output"
                    or "-o":
                        if (i + 1 < args.Length)
                        {
                            outputPath = args[++i];
                        }
                        break;
                    case "--exclude-public":
                        if (i + 1 < args.Length)
                        {
                            if (!bool.TryParse(args[++i], out excludePublic))
                            {
                                AnsiConsole.MarkupLine(
                                    "[red]Invalid value for --exclude-public. Expected 'true' or 'false'.[/]"
                                );
                                return 1;
                            }
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
                }
            }

            await ScanAsync(
                path,
                format,
                outputPath,
                excludePublic,
                skipUsings,
                fixUsings,
                unusedPackages,
                cts.Token
            );
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
        AnsiConsole.MarkupLine("  [cyan]path[/]               Path to .sln or .csproj file");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Options:[/]");
        AnsiConsole.MarkupLine(
            "  [cyan]--format, -f[/]       Output format: text or json (default: text)"
        );
        AnsiConsole.MarkupLine(
            "  [cyan]--output, -o[/]       Output file path (only for JSON format)"
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
    }

    private static async Task ScanAsync(
        string path,
        string format,
        string? outputPath,
        bool excludePublic,
        bool skipUsings,
        bool fixUsings,
        bool unusedPackages,
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
        var loader = new SolutionLoader();
        var solution = await loader.LoadAsync(path, progress, cancellationToken);

        // Index declared symbols
        cancellationToken.ThrowIfCancellationRequested();
        var indexer = new SymbolIndexer();
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
        var detector = new UnusedDetector(excludePublic);
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
            var packageResults = await packageAnalyzer.AnalyzeAsync(
                solution,
                progress,
                cancellationToken
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

        if (format.ToLowerInvariant() == "json")
        {
            var jsonPath = outputPath ?? "unused-code-report.json";
            await reporter.WriteJsonReportAsync(result, jsonPath);
        }
        else
        {
            reporter.WriteConsoleReport(result);
        }
    }
}
