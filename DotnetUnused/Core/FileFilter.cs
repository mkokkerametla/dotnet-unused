using System.Text.RegularExpressions;

namespace DotnetUnused.Core;

/// <summary>
/// Utility class for filtering files during analysis.
/// </summary>
public static partial class FileFilter
{
    /// <summary>
    /// Compiled regex for EF Core migration timestamp pattern (e.g., 20231226120000_Initial)
    /// Uses source generation for optimal performance
    /// </summary>
    [GeneratedRegex(@"\d{14}_", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MigrationTimestampPattern();

    /// <summary>
    /// Determines whether a file should be analyzed based on its path.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file should be analyzed; otherwise, false.</returns>
    public static bool ShouldAnalyze(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        // Only analyze .cs files (exclude .cshtml, .razor, .xaml, etc.)
        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Exclude bin/obj directories (all positions: start, middle, end)
        if (
            filePath.Contains("\\bin\\")
            || filePath.Contains("\\obj\\")
            || filePath.Contains("/bin/")
            || filePath.Contains("/obj/")
            || filePath.StartsWith("bin\\")
            || filePath.StartsWith("obj\\")
            || filePath.StartsWith("bin/")
            || filePath.StartsWith("obj/")
            || filePath.EndsWith("\\bin")
            || filePath.EndsWith("\\obj")
            || filePath.EndsWith("/bin")
            || filePath.EndsWith("/obj")
        )
        {
            return false;
        }

        // Exclude EF Core migrations (auto-generated, typically have timestamp patterns)
        // But allow user files in Migrations folder that don't match migration patterns
        if (
            (filePath.Contains("\\Migrations\\") || filePath.Contains("/Migrations/"))
            && (
                filePath.Contains("ModelSnapshot.cs")
                || MigrationTimestampPattern().IsMatch(filePath)
            )
        ) // Timestamp pattern like 20231226120000_
        {
            return false;
        }

        // Exclude generated files
        if (
            filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || filePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || filePath.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        return true;
    }
}
