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
            // Deconstruction assignment: (a, b) = expr; implicitly calls expr.Deconstruct(...).
            // There is no syntax node for that call, so record it explicitly.
            if (node.Left is TupleExpressionSyntax tuple)
            {
                MarkDeconstructAsReferenced(node.Right, tuple.Arguments.Count);
            }
            // Deconstruction declaration: var (a, b) = expr; parses as an
            // assignment whose left side is a declaration expression carrying
            // a parenthesized designation (not a local declaration statement).
            else if (
                node.Left is DeclarationExpressionSyntax declaration
                && declaration.Designation is ParenthesizedVariableDesignationSyntax designation
            )
            {
                MarkDeconstructAsReferenced(node.Right, designation.Variables.Count);
            }
            base.VisitAssignmentExpression(node);
        }

        public override void VisitUsingStatement(UsingStatementSyntax node)
        {
            // using (expr) implicitly calls expr.Dispose() (or DisposeAsync() for await using).
            // There is no syntax node for that call, so record it explicitly.
            if (node.Expression != null)
            {
                MarkDisposedAsReferenced(
                    node.Expression,
                    node.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword)
                );
            }
            else if (node.Declaration != null)
            {
                foreach (var variable in node.Declaration.Variables)
                {
                    if (variable.Initializer?.Value != null)
                    {
                        MarkDisposedAsReferenced(
                            variable.Initializer.Value,
                            node.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword)
                        );
                    }
                }
            }
            base.VisitUsingStatement(node);
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            // using var x = expr; implicitly calls expr.Dispose() (or DisposeAsync()).
            if (node.UsingKeyword.IsKind(SyntaxKind.UsingKeyword) && node.Declaration != null)
            {
                foreach (var variable in node.Declaration.Variables)
                {
                    if (variable.Initializer?.Value != null)
                    {
                        MarkDisposedAsReferenced(
                            variable.Initializer.Value,
                            node.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword)
                        );
                    }
                }
            }

            base.VisitLocalDeclarationStatement(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            // foreach implicitly calls GetEnumerator(), MoveNext(), and the Current
            // property. Record all three explicitly via the semantic model.
            AddForEachInfo(_semanticModel.GetForEachStatementInfo(node), node);
            base.VisitForEachStatement(node);
        }

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            var info = _semanticModel.GetForEachStatementInfo(node);
            AddForEachInfo(info, node);

            // foreach (var (a, b) in xs) additionally deconstructs each element.
            var designation = node.DescendantNodes()
                .OfType<ParenthesizedVariableDesignationSyntax>()
                .FirstOrDefault();
            if (designation != null)
            {
                var elementType = info.CurrentProperty?.Type;
                if (elementType != null)
                {
                    var deconstruct = FindMethod(
                        elementType,
                        "Deconstruct",
                        designation.Variables.Count
                    );
                    if (deconstruct != null)
                    {
                        AddReference(deconstruct, node);
                    }
                }
            }

            base.VisitForEachVariableStatement(node);
        }

        private void AddForEachInfo(ForEachStatementInfo info, SyntaxNode node)
        {
            if (info.GetEnumeratorMethod != null)
            {
                AddReference(info.GetEnumeratorMethod, node);
            }
            if (info.MoveNextMethod != null)
            {
                AddReference(info.MoveNextMethod, node);
            }
            if (info.CurrentProperty != null)
            {
                AddReference(info.CurrentProperty, node);
            }
        }

        private void MarkDisposedAsReferenced(ExpressionSyntax expression, bool awaitUsing)
        {
            var type = _semanticModel.GetTypeInfo(expression, _cancellationToken).Type;
            if (type == null)
            {
                return;
            }

            var methodName = awaitUsing ? "DisposeAsync" : "Dispose";
            var method = FindMethod(type, methodName, 0);
            if (method != null)
            {
                AddReference(method, expression);
            }
        }

        private void MarkDeconstructAsReferenced(ExpressionSyntax expression, int arity)
        {
            var type = _semanticModel.GetTypeInfo(expression, _cancellationToken).Type;
            if (type == null)
            {
                return;
            }

            var method = FindMethod(type, "Deconstruct", arity);
            if (method != null)
            {
                AddReference(method, expression);
            }
        }

        /// <summary>
        /// Finds a method by name and parameter count, walking the base-type chain
        /// so inherited methods (e.g. Dispose from a base class) are found too.
        /// </summary>
        private static IMethodSymbol? FindMethod(ITypeSymbol type, string name, int parameterCount)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current is not INamedTypeSymbol namedType)
                {
                    continue;
                }

                foreach (var member in namedType.GetMembers(name))
                {
                    if (
                        member is IMethodSymbol method
                        && method.Parameters.Length == parameterCount
                        && method.MethodKind == MethodKind.Ordinary
                    )
                    {
                        return method;
                    }
                }
            }

            return null;
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
                AddReference(symbol, node);
            }

            // Also check candidate symbols (for overload resolution)
            foreach (var candidate in symbolInfo.CandidateSymbols)
            {
                AddReference(candidate, node);
            }
        }

        /// <summary>
        /// Records a reference to a type, unless the reference sits inside that type's own
        /// declaration. A type that only refers to itself (recursion, factory methods) is
        /// still unused.
        /// </summary>
        private void AddTypeReference(INamedTypeSymbol type, SyntaxNode node)
        {
            // Constructed generics (List<int>) map back to their definition (List<T>)
            var definition = type.OriginalDefinition;

            foreach (var ancestor in node.Ancestors())
            {
                if (ancestor is not (BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
                {
                    continue;
                }

                var enclosing = _semanticModel.GetDeclaredSymbol(ancestor, _cancellationToken);
                if (SymbolEqualityComparer.Default.Equals(enclosing, definition))
                {
                    return;
                }
            }

            _references.Add(definition);
        }

        private void AddReference(ISymbol symbol, SyntaxNode node)
        {
            // Using a member (or constructing an instance) counts as using its type, so a type
            // that is only reached through its members is not reported as unused.
            if (symbol.Kind != SymbolKind.NamedType && symbol.ContainingType is { } owner)
            {
                AddTypeReference(owner, node);
            }

            if (symbol is INamedTypeSymbol namedType)
            {
                AddTypeReference(namedType, node);
                return;
            }

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

            // An extension method called as x.Ext() resolves to a reduced form; map it back to
            // the method that was declared
            if (symbol is IMethodSymbol { ReducedFrom: { } reducedFrom })
            {
                symbol = reducedFrom;
            }

            // Normalize constructed symbols to their definitions so they match
            // the declared symbols, which are always definitions. OriginalDefinition
            // preserves the signature: a call to Box<int>.Foo(string) maps to
            // Box<T>.Foo(string), not to the first overload named Foo. (A previous
            // name-only lookup on the original definition did exactly that, and
            // reported the called overload as unused while treating an uncalled
            // overload as used.) This also covers generic method instantiations
            // such as GetAsync<MyClass>() -> GetAsync<T>().
            if (!symbol.IsDefinition)
            {
                symbol = symbol.OriginalDefinition;
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
