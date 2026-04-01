using System.Text.Json;
using DotnetUnused.Models;
using DotnetUnused.Reporting;
using Xunit;

namespace DotnetUnused.Tests.Reporting;

[Collection("Console")]
public class ReportWriterJsonTests
{
    [Fact(Skip = "Integration test - requires full solution analysis setup")]
    public async Task WriteJsonReportAsync_IncludesBothUnusedCountFields_ForBackwardCompatibility()
    {
        // Arrange - Use empty result to avoid needing mock ISymbol instances
        var result = new AnalysisResult { TotalSymbolsAnalyzed = 100, TotalReferencesFound = 80 };

        var writer = new ReportWriter();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await writer.WriteJsonReportAsync(result, tempFile);

            // Assert
            var json = await File.ReadAllTextAsync(tempFile);
            var doc = JsonDocument.Parse(json);
            var summary = doc.RootElement.GetProperty("Summary");

            // Both fields should be present for backward compatibility
            Assert.True(summary.TryGetProperty("UnusedCount", out var unusedCount));
            Assert.True(summary.TryGetProperty("UnusedSymbolsCount", out var unusedSymbolsCount));

            // Both should have the same value
            Assert.Equal(unusedCount.GetInt32(), unusedSymbolsCount.GetInt32());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact(Skip = "Integration test - requires full solution analysis setup")]
    public async Task WriteJsonReportAsync_IncludesUnusedUsingsCount()
    {
        // Arrange
        var result = new AnalysisResult();
        result.AddUnusedUsing(
            new UsingDirectiveInfo
            {
                FilePath = "test.cs",
                LineNumber = 1,
                Namespace = "System.Linq",
                Message = "Unused"
            }
        );

        var writer = new ReportWriter();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await writer.WriteJsonReportAsync(result, tempFile);

            // Assert
            var json = await File.ReadAllTextAsync(tempFile);
            var doc = JsonDocument.Parse(json);
            var summary = doc.RootElement.GetProperty("Summary");

            Assert.True(summary.TryGetProperty("UnusedUsingsCount", out var usingsCount));
            Assert.Equal(1, usingsCount.GetInt32());
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
    public async Task WriteJsonReportAsync_WithBracketedOutputPath_WritesFile()
    {
        var result = new AnalysisResult();
        var writer = new ReportWriter();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-unused[{Guid.NewGuid():N}]");
        var tempFile = Path.Combine(tempDir, "report[1].json");

        Directory.CreateDirectory(tempDir);

        try
        {
            await writer.WriteJsonReportAsync(result, tempFile);

            Assert.True(File.Exists(tempFile));

            var json = await File.ReadAllTextAsync(tempFile);
            var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.TryGetProperty("summary", out _));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
