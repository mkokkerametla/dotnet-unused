using System.Text;
using System.Text.Json;
using DotnetUnused.Models;
using Spectre.Console;

namespace DotnetUnused.Reporting;

/// <summary>
/// Formats and outputs analysis results
/// </summary>
public sealed class ReportWriter
{
    /// <summary>
    /// Writes results to console in a human-readable format
    /// </summary>
    public void WriteConsoleReport(AnalysisResult result)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Rule("[yellow]Unused Code Analysis Results[/]").RuleStyle("grey").LeftJustified()
        );
        AnsiConsole.WriteLine();

        // Summary
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        summaryTable.AddRow("Total Symbols Analyzed", result.TotalSymbolsAnalyzed.ToString());
        summaryTable.AddRow("Total References Found", result.TotalReferencesFound.ToString());
        summaryTable.AddRow("Unused Symbols", $"[red]{result.UnusedSymbols.Count}[/]");
        summaryTable.AddRow("Unused Usings", $"[yellow]{result.UnusedUsings.Count}[/]");
        if (result.TotalPackagesAnalyzed > 0)
        {
            summaryTable.AddRow("Packages Analyzed", result.TotalPackagesAnalyzed.ToString());
            summaryTable.AddRow("Unused Packages", $"[orange1]{result.UnusedPackages.Count}[/]");
        }
        summaryTable.AddRow("Analysis Duration", $"{result.Duration.TotalSeconds:F2}s");

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();

        if (
            result.UnusedSymbols.Count == 0
            && result.UnusedUsings.Count == 0
            && result.UnusedPackages.Count == 0
        )
        {
            AnsiConsole.MarkupLine("[green]No unused code found![/]");
            return;
        }

        if (result.UnusedSymbols.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No unused symbols found![/]");
        }

        // Group by symbol kind
        var grouped = result.UnusedSymbols.GroupBy(s => s.Kind).OrderBy(g => g.Key.ToString());

        foreach (var group in grouped)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Rule($"[cyan]Unused {group.Key}s ({group.Count()})[/]")
                    .RuleStyle("grey")
                    .LeftJustified()
            );
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Name")
                .AddColumn("Location");

            foreach (var symbol in group.OrderBy(s => s.FilePath).ThenBy(s => s.LineNumber))
            {
                var name = symbol.FullyQualifiedName;
                var location = $"{symbol.FilePath}:{symbol.LineNumber}";
                table.AddRow(Markup.Escape(name), Markup.Escape(location));
            }

            AnsiConsole.Write(table);
        }

        // Display unused usings
        if (result.UnusedUsings.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Rule($"[yellow]Unused Using Directives ({result.UnusedUsings.Count})[/]")
                    .RuleStyle("grey")
                    .LeftJustified()
            );
            AnsiConsole.WriteLine();

            // Group by file
            var groupedByFile = result.UnusedUsings.GroupBy(u => u.FilePath).OrderBy(g => g.Key);

            var usingTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("File")
                .AddColumn("Line")
                .AddColumn("Using Directive");

            foreach (var fileGroup in groupedByFile)
            {
                var fileName = Path.GetFileName(fileGroup.Key);
                var isFirst = true;

                foreach (var unusedUsing in fileGroup.OrderBy(u => u.LineNumber))
                {
                    usingTable.AddRow(
                        isFirst ? Markup.Escape(fileName) : "",
                        unusedUsing.LineNumber.ToString(),
                        $"[dim]{Markup.Escape(unusedUsing.Namespace)}[/]"
                    );
                    isFirst = false;
                }
            }

            AnsiConsole.Write(usingTable);
        }

        // Display unused packages
        if (result.UnusedPackages.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Rule($"[orange1]Unused NuGet Packages ({result.UnusedPackages.Count})[/]")
                    .RuleStyle("grey")
                    .LeftJustified()
            );
            AnsiConsole.WriteLine();

            var packageTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Project")
                .AddColumn("Package")
                .AddColumn("Version");

            foreach (
                var pkg in result
                    .UnusedPackages.OrderBy(p => p.ProjectName)
                    .ThenBy(p => p.PackageId)
            )
            {
                packageTable.AddRow(
                    Markup.Escape(pkg.ProjectName),
                    Markup.Escape(pkg.PackageId),
                    Markup.Escape(pkg.Version)
                );
            }

            AnsiConsole.Write(packageTable);
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Writes results to a JSON file
    /// </summary>
    public async Task WriteJsonReportAsync(AnalysisResult result, string outputPath)
    {
        var jsonData = new
        {
            Summary = new
            {
                result.TotalSymbolsAnalyzed,
                result.TotalReferencesFound,
                UnusedCount = result.UnusedSymbols.Count, // Deprecated: Use UnusedSymbolsCount instead
                UnusedSymbolsCount = result.UnusedSymbols.Count,
                UnusedUsingsCount = result.UnusedUsings.Count,
                UnusedPackagesCount = result.UnusedPackages.Count,
                TotalPackagesAnalyzed = result.TotalPackagesAnalyzed,
                DurationSeconds = result.Duration.TotalSeconds
            },
            UnusedSymbols = result
                .UnusedSymbols.Select(s => new
                {
                    Kind = s.Kind.ToString(),
                    s.FullyQualifiedName,
                    s.FilePath,
                    s.LineNumber
                })
                .OrderBy(s => s.FilePath)
                .ThenBy(s => s.LineNumber)
                .ToList(),
            UnusedUsings = result
                .UnusedUsings.Select(u => new
                {
                    u.FilePath,
                    u.LineNumber,
                    u.Namespace,
                    u.Message
                })
                .OrderBy(u => u.FilePath)
                .ThenBy(u => u.LineNumber)
                .ToList(),
            UnusedPackages = result
                .UnusedPackages.Select(p => new
                {
                    p.ProjectName,
                    p.ProjectPath,
                    p.PackageId,
                    p.Version,
                    p.CandidateNamespaces
                })
                .OrderBy(p => p.ProjectName)
                .ThenBy(p => p.PackageId)
                .ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(jsonData, options);
        await File.WriteAllTextAsync(outputPath, json);

        AnsiConsole.MarkupLine($"[green]JSON report written to: {Markup.Escape(outputPath)}[/]");
    }

    /// <summary>
    /// Writes results to a Markdown file
    /// </summary>
    public async Task WriteMarkdownReportAsync(AnalysisResult result, string outputPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Unused Code Analysis Results");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| ------ | ----- |");
        sb.AppendLine($"| Total Symbols Analyzed | {result.TotalSymbolsAnalyzed} |");
        sb.AppendLine($"| Total References Found | {result.TotalReferencesFound} |");
        sb.AppendLine($"| Unused Symbols | {result.UnusedSymbols.Count} |");
        sb.AppendLine($"| Unused Usings | {result.UnusedUsings.Count} |");
        if (result.TotalPackagesAnalyzed > 0)
        {
            sb.AppendLine($"| Packages Analyzed | {result.TotalPackagesAnalyzed} |");
            sb.AppendLine($"| Unused Packages | {result.UnusedPackages.Count} |");
        }
        sb.AppendLine($"| Analysis Duration | {result.Duration.TotalSeconds:F2}s |");
        sb.AppendLine();

        if (
            result.UnusedSymbols.Count == 0
            && result.UnusedUsings.Count == 0
            && result.UnusedPackages.Count == 0
        )
        {
            sb.AppendLine("No unused code found!");
            await File.WriteAllTextAsync(outputPath, sb.ToString());
            AnsiConsole.MarkupLine(
                $"[green]Markdown report written to: {Markup.Escape(outputPath)}[/]"
            );
            return;
        }

        // Unused symbols grouped by kind
        var grouped = result.UnusedSymbols.GroupBy(s => s.Kind).OrderBy(g => g.Key.ToString());
        foreach (var group in grouped)
        {
            sb.AppendLine($"## Unused {group.Key}s ({group.Count()})");
            sb.AppendLine();
            sb.AppendLine("| Name | Location |");
            sb.AppendLine("| ---- | -------- |");
            foreach (var symbol in group.OrderBy(s => s.FilePath).ThenBy(s => s.LineNumber))
            {
                var location = $"{symbol.FilePath}:{symbol.LineNumber}";
                sb.AppendLine(
                    $"| {EscapeMarkdown(symbol.FullyQualifiedName)} | {EscapeMarkdown(location)} |"
                );
            }
            sb.AppendLine();
        }

        // Unused usings
        if (result.UnusedUsings.Count > 0)
        {
            sb.AppendLine($"## Unused Using Directives ({result.UnusedUsings.Count})");
            sb.AppendLine();
            sb.AppendLine("| File | Line | Using Directive |");
            sb.AppendLine("| ---- | ---- | --------------- |");
            foreach (
                var unusedUsing in result
                    .UnusedUsings.OrderBy(u => u.FilePath)
                    .ThenBy(u => u.LineNumber)
            )
            {
                var fileName = Path.GetFileName(unusedUsing.FilePath);
                sb.AppendLine(
                    $"| {EscapeMarkdown(fileName)} | {unusedUsing.LineNumber} | {EscapeMarkdown(unusedUsing.Namespace)} |"
                );
            }
            sb.AppendLine();
        }

        // Unused packages
        if (result.UnusedPackages.Count > 0)
        {
            sb.AppendLine($"## Unused NuGet Packages ({result.UnusedPackages.Count})");
            sb.AppendLine();
            sb.AppendLine("| Project | Package | Version |");
            sb.AppendLine("| ------- | ------- | ------- |");
            foreach (
                var pkg in result
                    .UnusedPackages.OrderBy(p => p.ProjectName)
                    .ThenBy(p => p.PackageId)
            )
            {
                sb.AppendLine(
                    $"| {EscapeMarkdown(pkg.ProjectName)} | {EscapeMarkdown(pkg.PackageId)} | {EscapeMarkdown(pkg.Version)} |"
                );
            }
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString());
        AnsiConsole.MarkupLine($"[green]Markdown report written to: {Markup.Escape(outputPath)}[/]");
    }

    /// <summary>
    /// Escapes characters that would break Markdown table cells
    /// </summary>
    private static string EscapeMarkdown(string value) =>
        value.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
}
