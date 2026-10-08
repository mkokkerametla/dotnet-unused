using System.Collections.Concurrent;
using DotnetUnused.Models;
using Microsoft.CodeAnalysis;

namespace DotnetUnused.Core;

/// <summary>
/// Computes unused symbols by subtracting referenced from declared
/// Applies heuristics to avoid false positives
/// </summary>
public sealed class UnusedDetector
{
    private readonly bool _excludePublicApi;
    private readonly HashSet<string> _ignoredAttributes;

    public UnusedDetector(bool excludePublicApi = true, IEnumerable<string>? ignoreAttributes = null)
    {
        _excludePublicApi = excludePublicApi;
        _ignoredAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (ignoreAttributes != null)
        {
            foreach (var attr in ignoreAttributes)
            {
                if (string.IsNullOrWhiteSpace(attr))
                {
                    continue;
                }

                // Store both "TestInitialize" and "TestInitializeAttribute" so we match the Roslyn
                // attribute class name whether or not it follows the "...Attribute" convention.
                var name = attr.Trim();
                _ignoredAttributes.Add(name);
                if (!name.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
                {
                    _ignoredAttributes.Add(name + "Attribute");
                }
            }
        }
    }

    /// <summary>
    /// Computes unused symbols: Declared - Referenced
    /// </summary>
    public AnalysisResult DetectUnused(
        ConcurrentBag<SymbolDefinition> declaredSymbols,
        ConcurrentBag<ISymbol> referencedSymbols,
        IProgress<string>? progress = null
    )
    {
        progress?.Report("Computing unused symbols...");

        var result = new AnalysisResult
        {
            TotalSymbolsAnalyzed = declaredSymbols.Count,
            TotalReferencesFound = referencedSymbols.Count
        };

        // Create a HashSet of referenced symbols for fast lookup
        var referencedSet = new HashSet<ISymbol>(referencedSymbols, SymbolEqualityComparer.Default);

        // Find symbols that are declared but not referenced
        foreach (var declared in declaredSymbols)
        {
            if (!referencedSet.Contains(declared.Symbol))
            {
                // Apply heuristics to avoid false positives
                if (ShouldReportAsUnused(declared.Symbol))
                {
                    result.AddUnusedSymbol(declared);
                }
            }
        }

        progress?.Report($"Found {result.UnusedSymbols.Count} unused symbols");
        return result;
    }

    /// <summary>
    /// Applies heuristics to determine if a symbol should be reported as unused
    /// </summary>
    private bool ShouldReportAsUnused(ISymbol symbol)
    {
        // Entry points - never report as unused
        if (IsEntryPoint(symbol))
        {
            return false;
        }

        // Public API - configurable
        if (_excludePublicApi && symbol.DeclaredAccessibility == Accessibility.Public)
        {
            return false;
        }

        // Test methods (including lifecycle hooks) - never report as unused
        if (IsTestMethod(symbol))
        {
            return false;
        }

        // User-specified ignored attributes (e.g. --ignore-attributes TestInitialize)
        if (HasIgnoredAttribute(symbol))
        {
            return false;
        }

        // ASP.NET Core endpoints/controllers - never report as unused
        if (IsAspNetCoreEndpoint(symbol))
        {
            return false;
        }

        // Serialization members - never report as unused
        if (IsSerializationMember(symbol))
        {
            return false;
        }

        // Explicitly marked as used - never report as unused
        if (HasUsedImplicitlyAttribute(symbol))
        {
            return false;
        }

        // XAML event handlers - never report as unused
        if (IsXamlEventHandler(symbol))
        {
            return false;
        }

        // Constructors used by DI - be conservative
        if (IsConstructor(symbol))
        {
            // Only report private constructors as unused
            return symbol.DeclaredAccessibility == Accessibility.Private;
        }

        return true;
    }

    private static bool IsEntryPoint(ISymbol symbol)
    {
        // Check for Main method
        if (symbol is IMethodSymbol method)
        {
            return method.Name == "Main" && method.IsStatic;
        }

        return false;
    }

    private static bool IsTestMethod(ISymbol symbol)
    {
        if (symbol is not IMethodSymbol method)
        {
            return false;
        }

        // Check for common test framework attributes, including setup/teardown lifecycle hooks
        // (MSTest TestInitialize/Cleanup, NUnit SetUp/TearDown, etc.) which are invoked by the
        // test runner rather than by direct code references.
        var attributes = method.GetAttributes();
        foreach (var attr in attributes)
        {
            var attrName = attr.AttributeClass?.Name;
            if (
                attrName
                is "FactAttribute"
                    or "TestAttribute"
                    or "TestMethodAttribute"
                    or "TheoryAttribute"
                    or "TestCaseAttribute"
                    // MSTest lifecycle
                    or "TestInitializeAttribute"
                    or "TestCleanupAttribute"
                    or "ClassInitializeAttribute"
                    or "ClassCleanupAttribute"
                    or "AssemblyInitializeAttribute"
                    or "AssemblyCleanupAttribute"
                    // NUnit lifecycle
                    or "SetUpAttribute"
                    or "TearDownAttribute"
                    or "OneTimeSetUpAttribute"
                    or "OneTimeTearDownAttribute"
            )
            {
                return true;
            }
        }

        return false;
    }

    private bool HasIgnoredAttribute(ISymbol symbol)
    {
        if (_ignoredAttributes.Count == 0)
        {
            return false;
        }

        foreach (var attr in symbol.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName != null && _ignoredAttributes.Contains(attrName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAspNetCoreEndpoint(ISymbol symbol)
    {
        // Check if the containing type is a controller
        var containingType = symbol.ContainingType;
        if (containingType != null)
        {
            // Check if it inherits from ControllerBase or Controller
            var baseType = containingType.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "ControllerBase" || baseType.Name == "Controller")
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }
        }

        // Check for endpoint attributes (Minimal API, Web API)
        if (symbol is IMethodSymbol method)
        {
            var attributes = method.GetAttributes();
            foreach (var attr in attributes)
            {
                var attrName = attr.AttributeClass?.Name;
                if (
                    attrName
                    is "HttpGetAttribute"
                        or "HttpPostAttribute"
                        or "HttpPutAttribute"
                        or "HttpDeleteAttribute"
                        or "HttpPatchAttribute"
                        or "RouteAttribute"
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSerializationMember(ISymbol symbol)
    {
        // Check for properties/fields used in serialization
        if (symbol is IPropertySymbol or IFieldSymbol)
        {
            var attributes = symbol.GetAttributes();
            foreach (var attr in attributes)
            {
                var attrName = attr.AttributeClass?.Name;
                if (
                    attrName
                    is "JsonPropertyNameAttribute"
                        or "JsonPropertyAttribute"
                        or "DataMemberAttribute"
                        or "XmlElementAttribute"
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasUsedImplicitlyAttribute(ISymbol symbol)
    {
        var attributes = symbol.GetAttributes();
        foreach (var attr in attributes)
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName is "UsedImplicitlyAttribute" or "PreserveAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsXamlEventHandler(ISymbol symbol)
    {
        // XAML event handlers are typically:
        // 1. Private/internal methods (not public)
        // 2. Return void
        // 3. Have typical event handler signatures (object sender, EventArgs e)
        // 4. Named with common patterns: Button_Click, OnLoaded, HandleSomething, etc.

        if (symbol is not IMethodSymbol method)
        {
            return false;
        }

        // Must return void
        if (method.ReturnsVoid == false)
        {
            return false;
        }

        // Check for common XAML/WPF event handler naming patterns
        var name = method.Name;
        if (
            name.Contains("_Click")
            || name.Contains("_OnClick")
            || name.Contains("_Loaded")
            || name.Contains("_OnLoaded")
            || name.Contains("_Changed")
            || name.Contains("_OnChanged")
            || name.Contains("_Checked")
            || name.Contains("_OnChecked")
            || name.Contains("_Selected")
            || name.Contains("_OnSelected")
            || name.Contains("_Closing")
            || name.Contains("_Closed")
            || name.StartsWith("On")
            || name.StartsWith("Handle")
            || name.EndsWith("Handler")
            || name.EndsWith("_Click")
            || name.EndsWith("_OnClick")
        )
        {
            // Check if it has event handler signature (sender, args) or no parameters
            var parameters = method.Parameters;
            if (
                parameters.Length == 0
                || (
                    parameters.Length == 2
                    && parameters[0].Type.Name == "Object"
                    && parameters[1].Type.Name.Contains("EventArgs")
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConstructor(ISymbol symbol)
    {
        return symbol is IMethodSymbol method && method.MethodKind == MethodKind.Constructor;
    }
}
