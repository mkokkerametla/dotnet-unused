using DotnetUnused.Models;
using DotnetUnused.Reporting;
using Xunit;

namespace DotnetUnused.Tests.Reporting;

[Collection("Console")]
public class ReportWriterMarkdownTests
{
    [Fact]
    public async Task WriteMarkdownReportAsync_WritesSummaryAndSections()
    {
        // Arrange
        var result = new AnalysisResult { TotalSymbolsAnalyzed = 10, TotalReferencesFound = 8 };
        result.AddUnusedUsing(
            new UsingDirectiveInfo
            {
                FilePath = "/src/Foo.cs",
                LineNumber = 3,
                Namespace = "System.Linq",
                Message = "Unused"
            }
        );
        result.TotalPackagesAnalyzed = 2;
        result.AddUnusedPackage(
            new UnusedPackageInfo
            {
                PackageId = "Serilog",
                Version = "3.1.1",
                ProjectName = "MyApp",
                ProjectPath = "MyApp.csproj"
            }
        );

        var writer = new ReportWriter();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await writer.WriteMarkdownReportAsync(result, tempFile);

            // Assert
            var md = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("# Unused Code Analysis Results", md);
            Assert.Contains("## Summary", md);
            Assert.Contains("| Total Symbols Analyzed | 10 |", md);
            Assert.Contains("## Unused Using Directives (1)", md);
            Assert.Contains("System.Linq", md);
            Assert.Contains("## Unused NuGet Packages (1)", md);
            Assert.Contains("Serilog", md);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task WriteMarkdownReportAsync_EscapesPipeCharacters()
    {
        // Arrange
        var result = new AnalysisResult();
        result.AddUnusedUsing(
            new UsingDirectiveInfo
            {
                FilePath = "/src/Bar.cs",
                LineNumber = 1,
                Namespace = "Weird|Namespace",
                Message = "Unused"
            }
        );

        var writer = new ReportWriter();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await writer.WriteMarkdownReportAsync(result, tempFile);

            // Assert - a literal pipe must be escaped so it does not break the table.
            var md = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Weird\\|Namespace", md);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
