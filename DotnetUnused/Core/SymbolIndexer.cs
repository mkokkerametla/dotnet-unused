using System.Collections.Concurrent;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetUnused.Core;

/// <summary>
/// Scans all documents to find declared symbols (Methods, Properties, Fields)
/// </summary>
public sealed class SymbolIndexer
{
    /// <summary>
    /// Collects all declared symbols from the solution
    /// </summary>
    public async Task<ConcurrentBag<SymbolDefinition>> CollectDeclaredSymbolsAsync(
        Solution solution,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var declaredSymbols = new ConcurrentBag<SymbolDefinition>();

        var projects = solution.Projects.ToList();
        progress?.Report($"Indexing symbols from {projects.Count} projects...");

        // Process projects in parallel
        await Parallel.ForEachAsync(
            projects,
            cancellationToken,
            async (project, ct) =>
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation == null)
                {
                    return;
                }

                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var filePath = syntaxTree.FilePath;

                    // Skip generated files
                    if (!ShouldAnalyze(filePath))
                    {
                        continue;
                    }

                    var root = await syntaxTree.GetRootAsync(ct);
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);

                    // Walk the syntax tree and collect declarations
                    var declarations = root.DescendantNodes().ToList();

                    foreach (var node in declarations)
                    {
                        ISymbol? symbol = null;

                        switch (node)
                        {
                            case MethodDeclarationSyntax method:
                                symbol = semanticModel.GetDeclaredSymbol(method, ct);
                                break;
                            case PropertyDeclarationSyntax property:
                                symbol = semanticModel.GetDeclaredSymbol(property, ct);
                                break;
                            case FieldDeclarationSyntax field:
                                // Fields can have multiple variable declarators
                                foreach (var variable in field.Declaration.Variables)
                                {
                                    var fieldSymbol = semanticModel.GetDeclaredSymbol(variable, ct);
                                    if (fieldSymbol != null && ShouldIndexSymbol(fieldSymbol))
                                    {
                                        declaredSymbols.Add(
                                            new SymbolDefinition(
                                                fieldSymbol,
                                                variable.GetLocation()
                                            )
                                        );
                                    }
                                }
                                continue; // Skip the default handling
                        }

                        if (symbol != null && ShouldIndexSymbol(symbol))
                        {
                            declaredSymbols.Add(new SymbolDefinition(symbol, node.GetLocation()));
                        }
                    }
                }
            }
        );

        progress?.Report($"Found {declaredSymbols.Count} declared symbols");
        return declaredSymbols;
    }

    private static bool ShouldAnalyze(string filePath) => FileFilter.ShouldAnalyze(filePath);

    /// <summary>
    /// Determines if a symbol should be indexed for analysis
    /// </summary>
    private static bool ShouldIndexSymbol(ISymbol? symbol)
    {
        if (symbol == null)
        {
            return false;
        }

        // Filter out symbols we should never consider "unused"
        // These are filtered early to avoid false positives

        // Skip interface members (they're always "used" by implementations)
        if (symbol.ContainingType?.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        // Skip abstract members
        if (symbol.IsAbstract)
        {
            return false;
        }

        // Skip overrides (base class usage counts)
        if (symbol.IsOverride)
        {
            return false;
        }

        // Skip extern members
        if (symbol.IsExtern)
        {
            return false;
        }

        // Skip virtual members (might be overridden)
        if (symbol.IsVirtual)
        {
            return false;
        }

        // Only index method, property, and field symbols
        if (
            symbol.Kind != SymbolKind.Method
            && symbol.Kind != SymbolKind.Property
            && symbol.Kind != SymbolKind.Field
        )
        {
            return false;
        }

        return true;
    }
}
