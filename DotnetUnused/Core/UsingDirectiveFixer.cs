using DotnetUnused.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetUnused.Core;

/// <summary>
/// Applies fixes to remove unused using directives from source files
/// </summary>
public sealed class UsingDirectiveFixer
{
    /// <summary>
    /// Removes unused using directives from files
    /// </summary>
    public async Task<int> ApplyFixesAsync(
        Solution solution,
        List<UsingDirectiveInfo> unusedUsings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (unusedUsings.Count == 0)
        {
            return 0;
        }

        // Group usings by file for efficient processing
        var usingsByFile = unusedUsings
            .GroupBy(u => u.FilePath)
            .ToDictionary(g => g.Key, g => g.ToList());

        var filesModified = 0;
        var usingsRemoved = 0;

        foreach (var kvp in usingsByFile)
        {
            var filePath = kvp.Key;
            var usingsToRemove = kvp.Value;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Find the document in the solution
                var document = FindDocument(solution, filePath);
                if (document == null)
                {
                    progress?.Report(
                        $"Warning: Could not find document for {Path.GetFileName(filePath)}"
                    );
                    continue;
                }

                // Get syntax tree and root
                var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
                if (syntaxTree == null)
                {
                    continue;
                }

                var root = await syntaxTree.GetRootAsync(cancellationToken);
                var compilationUnit = root as CompilationUnitSyntax;
                if (compilationUnit == null)
                {
                    continue;
                }

                // Find and collect all using directives to remove
                var usingsToRemoveLines = usingsToRemove.Select(u => u.LineNumber).ToHashSet();

                // Collect all using nodes (both at compilation unit level and in namespaces)
                var allUsingNodes = root.DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Where(u =>
                    {
                        var lineNumber = u.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        return usingsToRemoveLines.Contains(lineNumber);
                    })
                    .ToList();

                if (!allUsingNodes.Any())
                {
                    progress?.Report(
                        $"Warning: Could not find using nodes to remove in {Path.GetFileName(filePath)}"
                    );
                    continue;
                }

                // Remove all unused usings in one operation to preserve formatting
                var newRoot = root.RemoveNodes(allUsingNodes, SyntaxRemoveOptions.KeepNoTrivia);

                if (newRoot == null)
                {
                    progress?.Report(
                        $"Warning: Failed to remove usings from {Path.GetFileName(filePath)}"
                    );
                    continue;
                }

                // Write back to file with original formatting preserved using atomic write
                var newText = newRoot.ToFullString();
                await WriteFileAtomicallyAsync(filePath, newText, cancellationToken);

                filesModified++;
                usingsRemoved += usingsToRemove.Count;

                progress?.Report(
                    $"Fixed {Path.GetFileName(filePath)}: removed {usingsToRemove.Count} unused usings"
                );
            }
            catch (Exception ex)
            {
                progress?.Report($"Error fixing {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        progress?.Report($"Removed {usingsRemoved} unused usings from {filesModified} files");
        return filesModified;
    }

    private static Document? FindDocument(Solution solution, string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (
                    document.FilePath != null
                    && Path.GetFullPath(document.FilePath)
                        .Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return document;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Writes content to a file atomically using temp file + rename pattern
    /// This prevents file corruption if write fails or is cancelled
    /// </summary>
    private static async Task WriteFileAtomicallyAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken
    )
    {
        string? tempFile = null;
        try
        {
            // Create temp file in same directory to ensure same volume (required for atomic move)
            var directory = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
            tempFile = Path.Combine(
                directory,
                $".{Path.GetFileName(filePath)}.{Guid.NewGuid()}.tmp"
            );

            // Write to temp file
            await File.WriteAllTextAsync(tempFile, content, cancellationToken);

            // Atomic replace: this is an atomic operation on Windows and Unix
            File.Move(tempFile, filePath, overwrite: true);
            tempFile = null; // Successfully moved, don't delete in finally
        }
        finally
        {
            // Clean up temp file if it still exists (write failed)
            if (tempFile != null && File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
