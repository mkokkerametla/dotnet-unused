using DotnetUnused.Core;
using DotnetUnused.Models;
using Xunit;

namespace DotnetUnused.Tests.Core;

public class UsingDirectiveFixerTests
{
    [Fact]
    public async Task ApplyFixesAsync_WithEmptyList_ReturnsZero()
    {
        // Arrange
        var fixer = new UsingDirectiveFixer();
        var solution = CreateEmptySolution();
        var unusedUsings = new List<UsingDirectiveInfo>();

        // Act
        var result = await fixer.ApplyFixesAsync(solution, unusedUsings);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ApplyFixesAsync_WithNonExistentFile_ReturnsZero()
    {
        // Arrange
        var fixer = new UsingDirectiveFixer();
        var solution = CreateEmptySolution();
        var unusedUsings = new List<UsingDirectiveInfo>
        {
            new()
            {
                FilePath = "test.cs",
                LineNumber = 1,
                Namespace = "System.Linq"
            }
        };

        // Act - File doesn't exist in solution, so nothing should be fixed
        var result = await fixer.ApplyFixesAsync(solution, unusedUsings);

        // Assert - Returns 0 because no files were modified
        Assert.Equal(0, result);
    }

    [Fact(Skip = "Requires real file system access - integration test")]
    public async Task ApplyFixesAsync_UsesAtomicWrite()
    {
        // This would test that files are written atomically using temp files
        // Requires actual file system operations
        await Task.CompletedTask;
    }

    private static Microsoft.CodeAnalysis.Solution CreateEmptySolution()
    {
        return new Microsoft.CodeAnalysis.AdhocWorkspace().CurrentSolution;
    }
}
