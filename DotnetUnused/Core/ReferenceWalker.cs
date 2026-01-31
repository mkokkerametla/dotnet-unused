using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotnetUnused.Core;

/// <summary>
/// Scans all documents to find referenced symbols
/// Uses a single-pass syntax tree traversal for performance
/// </summary>
public sealed class ReferenceWalker
{
    /// <summary>
    /// Finds all referenced symbols in the solution
    /// </summary>
    public async Task<ConcurrentBag<ISymbol>> CollectReferencedSymbolsAsync(
        Solution solution,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var referencedSymbols = new ConcurrentBag<ISymbol>();

        var projects = solution.Projects.ToList();
        progress?.Report($"Finding symbol references in {projects.Count} projects...");

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

                    // Single-pass traversal of the syntax tree
                    var walker = new SymbolReferenceWalker(semanticModel, referencedSymbols, ct);
                    walker.Visit(root);
                }
            }
        );

        progress?.Report($"Found {referencedSymbols.Count} symbol references");
        return referencedSymbols;
    }

    private static bool ShouldAnalyze(string filePath) => FileFilter.ShouldAnalyze(filePath);

    /// <summary>
    /// Internal walker that visits syntax nodes and collects symbol references
    /// </summary>
    private sealed class SymbolReferenceWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly ConcurrentBag<ISymbol> _references;
        private readonly CancellationToken _cancellationToken;

        public SymbolReferenceWalker(
            SemanticModel semanticModel,
            ConcurrentBag<ISymbol> references,
            CancellationToken cancellationToken
        )
        {
            _semanticModel = semanticModel;
            _references = references;
            _cancellationToken = cancellationToken;
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            ProcessNode(node);
            base.VisitIdentifierName(node);
        }

        public override void VisitGenericName(GenericNameSyntax node)
        {
            ProcessNode(node);
            base.VisitGenericName(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            ProcessNode(node);
            base.VisitObjectCreationExpression(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            ProcessNode(node);
            base.VisitInvocationExpression(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            ProcessNode(node);
            base.VisitMemberAccessExpression(node);
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            ProcessNode(node);
            base.VisitAttribute(node);
        }

        public override void VisitDeclarationPattern(DeclarationPatternSyntax node)
        {
            ProcessNode(node);
            base.VisitDeclarationPattern(node);
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            ProcessNode(node);
            base.VisitAssignmentExpression(node);
        }

        private void ProcessNode(SyntaxNode node)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var symbolInfo = _semanticModel.GetSymbolInfo(node, _cancellationToken);

            // Get the referenced symbol
            var symbol = symbolInfo.Symbol;
            if (symbol != null)
            {
                AddReference(symbol);
            }

            // Also check candidate symbols (for overload resolution)
            foreach (var candidate in symbolInfo.CandidateSymbols)
            {
                AddReference(candidate);
            }
        }

        private void AddReference(ISymbol symbol)
        {
            // Unwrap property accessors to their containing property
            if (
                symbol is IMethodSymbol method
                && (
                    method.MethodKind == MethodKind.PropertyGet
                    || method.MethodKind == MethodKind.PropertySet
                )
            )
            {
                symbol = method.AssociatedSymbol ?? symbol;
            }

            // Unwrap constructed generic methods to their original definitions
            // e.g., GetAsync<MyClass>() should map to GetAsync<T>()
            if (
                symbol is IMethodSymbol methodSymbol
                && methodSymbol.IsGenericMethod
                && !methodSymbol.IsDefinition
            )
            {
                symbol = methodSymbol.OriginalDefinition;
            }

            // Unwrap methods/properties/fields of constructed generic types
            // e.g., List<int>.Add should map to List<T>.Add
            if (
                symbol.ContainingType is INamedTypeSymbol containingType
                && containingType.IsGenericType
                && !containingType.IsDefinition
            )
            {
                var originalType = containingType.OriginalDefinition;

                // Find the corresponding member in the original definition
                var originalMember = originalType
                    .GetMembers(symbol.Name)
                    .FirstOrDefault(m => m.Kind == symbol.Kind);

                if (originalMember != null)
                {
                    symbol = originalMember;
                }
            }

            // Only track methods, properties, and fields
            if (
                symbol.Kind == SymbolKind.Method
                || symbol.Kind == SymbolKind.Property
                || symbol.Kind == SymbolKind.Field
            )
            {
                _references.Add(symbol);
            }
        }
    }
}
