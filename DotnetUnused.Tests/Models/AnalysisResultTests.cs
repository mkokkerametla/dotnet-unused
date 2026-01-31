using System;
using System.Linq;
using DotnetUnused.Models;
using Xunit;

namespace DotnetUnused.Tests.Models;

public class AnalysisResultTests
{
    [Fact]
    public void Constructor_InitializesEmptyUnusedSymbolsList()
    {
        // Act
        var result = new AnalysisResult();

        // Assert
        Assert.NotNull(result.UnusedSymbols);
        Assert.Empty(result.UnusedSymbols);
    }

    [Fact]
    public void Constructor_InitializesPropertiesWithDefaultValues()
    {
        // Act
        var result = new AnalysisResult();

        // Assert
        Assert.Equal(TimeSpan.Zero, result.Duration);
        Assert.Equal(0, result.TotalSymbolsAnalyzed);
        Assert.Equal(0, result.TotalReferencesFound);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var result = new AnalysisResult();
        var duration = TimeSpan.FromSeconds(5.5);

        // Act
        result.Duration = duration;
        result.TotalSymbolsAnalyzed = 100;
        result.TotalReferencesFound = 85;

        // Assert
        Assert.Equal(duration, result.Duration);
        Assert.Equal(100, result.TotalSymbolsAnalyzed);
        Assert.Equal(85, result.TotalReferencesFound);
    }

    [Fact]
    public void TotalSymbolsAnalyzed_CanBeZero()
    {
        // Arrange
        var result = new AnalysisResult { TotalSymbolsAnalyzed = 0 };

        // Assert
        Assert.Equal(0, result.TotalSymbolsAnalyzed);
    }

    [Fact]
    public void TotalReferencesFound_CanExceedTotalSymbolsAnalyzed()
    {
        // Arrange - can have more references than symbols (multiple refs to same symbol)
        var result = new AnalysisResult { TotalSymbolsAnalyzed = 50, TotalReferencesFound = 500 };

        // Assert
        Assert.Equal(50, result.TotalSymbolsAnalyzed);
        Assert.Equal(500, result.TotalReferencesFound);
    }

    [Fact]
    public void Duration_CanHandleVeryLongAnalysis()
    {
        // Arrange
        var result = new AnalysisResult();
        var longDuration = TimeSpan.FromHours(2.5);

        // Act
        result.Duration = longDuration;

        // Assert
        Assert.Equal(longDuration, result.Duration);
    }

    [Fact]
    public void Duration_CanHandleVeryShortAnalysis()
    {
        // Arrange
        var result = new AnalysisResult();
        var shortDuration = TimeSpan.FromMilliseconds(50);

        // Act
        result.Duration = shortDuration;

        // Assert
        Assert.Equal(shortDuration, result.Duration);
    }

    [Fact]
    public void UnusedSymbols_IsNotNull_AfterConstruction()
    {
        // Act
        var result = new AnalysisResult();

        // Assert - should never be null, always initialized
        Assert.NotNull(result.UnusedSymbols);
    }

    [Fact]
    public void UnusedUsings_IsNotNull_AfterConstruction()
    {
        // Act
        var result = new AnalysisResult();

        // Assert - should never be null, always initialized
        Assert.NotNull(result.UnusedUsings);
        Assert.Empty(result.UnusedUsings);
    }

    [Fact]
    public void AddUnusedUsing_AddsToCollection()
    {
        // Arrange
        var result = new AnalysisResult();
        var usingInfo = new UsingDirectiveInfo
        {
            FilePath = "Test.cs",
            LineNumber = 1,
            Namespace = "System.Linq"
        };

        // Act
        result.AddUnusedUsing(usingInfo);

        // Assert
        Assert.Single(result.UnusedUsings);
        Assert.Equal("System.Linq", result.UnusedUsings[0].Namespace);
    }

    [Fact]
    public void UnusedUsings_CanContainMultipleEntries()
    {
        // Arrange
        var result = new AnalysisResult();

        // Act
        result.AddUnusedUsing(new UsingDirectiveInfo { Namespace = "System.Linq" });
        result.AddUnusedUsing(new UsingDirectiveInfo { Namespace = "System.Collections" });
        result.AddUnusedUsing(new UsingDirectiveInfo { Namespace = "System.Threading" });

        // Assert
        Assert.Equal(3, result.UnusedUsings.Count);
    }
}
