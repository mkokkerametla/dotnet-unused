using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace DotnetUnused.Core;

/// <summary>
/// Scans source files for namespace usages using regex-based heuristics.
/// </summary>
public sealed partial class NamespaceUsageScanner
{
    /// <summary>
    /// Regex for standard using statements: using Namespace;
    /// Handles 'global using' and 'using static' variations.
    /// </summary>
    [GeneratedRegex(
        @"^\s*(?:global\s+)?using\s+(?:static\s+)?([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\s*;",
        RegexOptions.Multiline | RegexOptions.Compiled
    )]
    private static partial Regex UsingStatementRegex();

    /// <summary>
    /// Regex for using aliases: using Alias = Namespace;
    /// </summary>
    [GeneratedRegex(
        @"^\s*(?:global\s+)?using\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)\s*;",
        RegexOptions.Multiline | RegexOptions.Compiled
    )]
    private static partial Regex UsingAliasRegex();

    /// <summary>
    /// Regex for fully qualified names used in code.
    /// Matches patterns like Namespace.Class.Method or new Namespace.Class()
    /// </summary>
    [GeneratedRegex(
        @"(?:new\s+)?([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)+)\s*[\.\(\<\[\{]",
        RegexOptions.Compiled
    )]
    private static partial Regex QualifiedNameRegex();

    /// <summary>
    /// Regex for attribute usages: [Namespace.Attribute] or [AttributeName]
    /// </summary>
    [GeneratedRegex(
        @"\[\s*([a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)",
        RegexOptions.Compiled
    )]
    private static partial Regex AttributeRegex();

    /// <summary>
    /// Scans all source files in a solution and returns found namespace usages.
    /// </summary>
    public async Task<HashSet<string>> ScanAsync(
        Solution solution,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var foundNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!SolutionLoader.ShouldAnalyzeDocument(document))
                {
                    continue;
                }

                var sourceText = await document.GetTextAsync(cancellationToken);
                var content = sourceText.ToString();

                ScanContent(content, foundNamespaces);
            }
        }

        progress?.Report($"Found {foundNamespaces.Count} unique namespace usages");
        return foundNamespaces;
    }

    /// <summary>
    /// Scans a single file content for namespace usages.
    /// </summary>
    public void ScanContent(string content, HashSet<string> foundNamespaces)
    {
        // Extract using statements
        foreach (Match match in UsingStatementRegex().Matches(content))
        {
            var ns = match.Groups[1].Value;
            AddNamespaceAndPrefixes(ns, foundNamespaces);
        }

        // Extract using aliases
        foreach (Match match in UsingAliasRegex().Matches(content))
        {
            var ns = match.Groups[2].Value;
            AddNamespaceAndPrefixes(ns, foundNamespaces);
        }

        // Extract qualified names
        foreach (Match match in QualifiedNameRegex().Matches(content))
        {
            var qualifiedName = match.Groups[1].Value;
            // Only add if it looks like a namespace (has dots and starts with uppercase)
            if (qualifiedName.Contains('.') && char.IsUpper(qualifiedName[0]))
            {
                AddNamespaceAndPrefixes(qualifiedName, foundNamespaces);
            }
        }

        // Extract attributes
        foreach (Match match in AttributeRegex().Matches(content))
        {
            var attrName = match.Groups[1].Value;
            if (attrName.Contains('.'))
            {
                AddNamespaceAndPrefixes(attrName, foundNamespaces);
            }
        }
    }

    /// <summary>
    /// Adds a namespace and all its prefix namespaces to the set.
    /// e.g., "System.Text.Json" adds "System", "System.Text", "System.Text.Json"
    /// </summary>
    private static void AddNamespaceAndPrefixes(string fullNamespace, HashSet<string> namespaces)
    {
        namespaces.Add(fullNamespace);

        // Add all prefix namespaces
        var parts = fullNamespace.Split('.');
        for (int i = 1; i < parts.Length; i++)
        {
            var prefix = string.Join('.', parts.Take(i));
            namespaces.Add(prefix);
        }
    }
}
