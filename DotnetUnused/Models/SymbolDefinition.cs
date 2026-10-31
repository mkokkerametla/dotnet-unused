using Microsoft.CodeAnalysis;

namespace DotnetUnused.Models;

/// <summary>
/// Represents a declared symbol with its location information
/// </summary>
public sealed class SymbolDefinition
{
    public ISymbol Symbol { get; }
    public string FilePath { get; }
    public int LineNumber { get; }
    public SymbolKind Kind { get; }
    public string FullyQualifiedName { get; }

    /// <summary>
    /// Human-readable kind: the type kind for types (Class, Struct, ...), otherwise the symbol kind.
    /// </summary>
    public string DisplayKind { get; }

    public SymbolDefinition(ISymbol symbol, Location location)
    {
        Symbol = symbol;
        var lineSpan = location.GetLineSpan();
        FilePath = lineSpan.Path;
        LineNumber = lineSpan.StartLinePosition.Line + 1; // Convert to 1-based
        Kind = symbol.Kind;
        DisplayKind = GetDisplayKind(symbol);
        FullyQualifiedName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string GetDisplayKind(ISymbol symbol)
    {
        if (symbol is not INamedTypeSymbol type)
        {
            return symbol.Kind.ToString();
        }

        return type.TypeKind switch
        {
            TypeKind.Class when type.IsRecord => "Record",
            TypeKind.Struct when type.IsRecord => "Record",
            TypeKind.Class => "Class",
            TypeKind.Struct => "Struct",
            TypeKind.Interface => "Interface",
            TypeKind.Enum => "Enum",
            TypeKind.Delegate => "Delegate",
            _ => "Type"
        };
    }

    public override string ToString() => $"{Kind} {FullyQualifiedName} at {FilePath}:{LineNumber}";
}
