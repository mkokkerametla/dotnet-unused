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

    public UnusedDetector(
        bool excludePublicApi = true,
        IEnumerable<string>? ignoreAttributes = null
    )
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
            TotalReferencesFound = referencedSymbols.Count,
        };

        // Create a HashSet of referenced symbols for fast lookup
        var referencedSet = new HashSet<ISymbol>(referencedSymbols, SymbolEqualityComparer.Default);

        // A call through an interface resolves to the interface member, not to
        // the method or property that implements it. Without this expansion,
        // every interface implementation called only through its interface
        // (including every explicit implementation, which can only be reached
        // that way) would be reported as unused. So: if an interface member is
        // referenced anywhere, every declared implementation of it counts as
        // referenced too. Implementations whose interface member is never
        // referenced are still reported, so true positives are preserved.
        // References are stored normalized to their original definitions, so
        // normalize the interface member the same way before comparing:
        // a call through IProgress<string> is stored as IProgress<T>.Report.
        // Members of interfaces declared outside the solution (IEnumerable,
        // IComparer<T>, ...) are mostly called by framework code the walker never
        // sees, so implementations of those always count as referenced.
        foreach (var declared in declaredSymbols)
        {
            foreach (var interfaceMember in GetImplementedInterfaceMembers(declared.Symbol))
            {
                var memberToCheck = interfaceMember.IsDefinition
                    ? interfaceMember
                    : interfaceMember.OriginalDefinition;
                if (
                    memberToCheck.Locations.Any(l => l.IsInMetadata)
                    || referencedSet.Contains(memberToCheck)
                )
                {
                    referencedSet.Add(declared.Symbol);
                    break;
                }
            }
        }

        // Find symbols that are declared but not referenced. A multi-targeted project is loaded
        // once per target framework, so the same declaration can appear several times with
        // distinct symbols. Report it once, and only if it is unused in every target.
        var declarations = declaredSymbols
            .GroupBy(d => (d.FilePath, d.LineNumber, d.FullyQualifiedName))
            .OrderBy(g => g.Key.FilePath, StringComparer.Ordinal)
            .ThenBy(g => g.Key.LineNumber);

        foreach (var declaration in declarations)
        {
            var isUnused = declaration.All(d =>
                !referencedSet.Contains(d.Symbol)
                // Apply heuristics to avoid false positives
                && ShouldReportAsUnused(d.Symbol)
            );

            if (isUnused)
            {
                result.AddUnusedSymbol(declaration.First());
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
        if (symbol is INamedTypeSymbol type)
        {
            return ShouldReportTypeAsUnused(type);
        }

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

    /// <summary>
    /// Applies heuristics to a type that no code in the solution refers to.
    /// </summary>
    private bool ShouldReportTypeAsUnused(INamedTypeSymbol type)
    {
        // Public API - configurable
        if (_excludePublicApi && type.DeclaredAccessibility == Accessibility.Public)
        {
            return false;
        }

        if (
            HasIgnoredAttribute(type)
            || HasUsedImplicitlyAttribute(type)
            || ContainsEntryPoint(type)
            || IsTestType(type)
            || IsInstantiatedByFramework(type)
        )
        {
            return false;
        }

        return true;
    }

    private static bool ContainsEntryPoint(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IMethodSymbol>().Any(IsEntryPoint);

    private static bool IsTestType(INamedTypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (
                attr.AttributeClass?.Name
                is "TestClassAttribute"
                    or "TestFixtureAttribute"
                    or "CollectionDefinitionAttribute"
            )
            {
                return true;
            }
        }

        // xUnit and friends have no class-level attribute; a type with test methods is a test class
        return type.GetMembers().OfType<IMethodSymbol>().Any(IsTestMethod);
    }

    /// <summary>
    /// Types that derive from a framework class or implement a framework interface are
    /// commonly created by the runtime (controllers, hosted services, middleware, converters,
    /// DbContexts, ...) with no reference in source, so they are never reported.
    /// </summary>
    private static bool IsInstantiatedByFramework(INamedTypeSymbol type)
    {
        // Delegates and enums inherit framework interfaces (ISerializable, ...) implicitly
        if (type.TypeKind is TypeKind.Delegate or TypeKind.Enum)
        {
            return false;
        }

        // Interfaces that come with an attribute or exception base class (Exception implements
        // ISerializable) say nothing about framework activation
        IEnumerable<INamedTypeSymbol> inheritedInterfaces = [];

        for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (
                baseType.Locations.All(l => !l.IsInMetadata)
                || baseType.SpecialType
                    is SpecialType.System_Object
                        or SpecialType.System_ValueType
                        or SpecialType.System_Enum
                        or SpecialType.System_MulticastDelegate
                        or SpecialType.System_Delegate
            )
            {
                continue;
            }

            // Attribute and exception types (including those derived from framework exceptions
            // such as InvalidOperationException) are only ever used by being named in code
            if (IsAttributeOrException(baseType))
            {
                inheritedInterfaces = baseType.AllInterfaces;
                break;
            }

            return true;
        }

        return type
            .AllInterfaces.Except(inheritedInterfaces, SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .Any(i =>
                i.Locations.Any(l => l.IsInMetadata)
                && i.ContainingNamespace?.ToDisplayString() is not ("System" or null)
                && !i.ContainingNamespace.ToDisplayString().StartsWith("System.Collections")
            );
    }

    private static bool IsAttributeOrException(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (
                current.Name is "Attribute" or "Exception"
                && current.ContainingNamespace?.ToDisplayString() == "System"
            )
            {
                return true;
            }
        }

        return false;
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
        //
        // NOTE: this is deliberately name-based and broad (any private void
        // method named On* with a matching signature is spared). XAML bindings
        // are not visible to the reference walker, so a name heuristic is the
        // only way to avoid flagging handlers; the cost is that a genuinely
        // unused helper with an event-handler-like name is also spared. That
        // false-negative tradeoff is intentional for a cleanup tool.

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

    /// <summary>
    /// Yields the interface members (methods or properties) that a declared
    /// symbol implements, if any. Covers both implicit implementations (a
    /// public method with a matching signature, found via
    /// <see cref="INamedTypeSymbol.FindImplementationForInterfaceMember"/>)
    /// and explicit implementations (e.g. <c>void IDisposable.Dispose()</c>).
    /// Explicit implementations must be read off
    /// <c>ExplicitInterfaceImplementations</c> directly: their
    /// <see cref="ISymbol.Name"/> is the qualified
    /// <c>"System.IDisposable.Dispose"</c>, so a name lookup on the interface
    /// finds nothing.
    /// </summary>
    private static IEnumerable<ISymbol> GetImplementedInterfaceMembers(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method)
        {
            foreach (var explicitImpl in method.ExplicitInterfaceImplementations)
            {
                yield return explicitImpl;
            }
        }
        else if (symbol is IPropertySymbol property)
        {
            foreach (var explicitImpl in property.ExplicitInterfaceImplementations)
            {
                yield return explicitImpl;
            }
        }
        else
        {
            yield break;
        }

        var containingType = symbol.ContainingType;
        if (containingType == null)
        {
            yield break;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers(symbol.Name))
            {
                if (
                    member.Kind == symbol.Kind
                    && SymbolEqualityComparer.Default.Equals(
                        containingType.FindImplementationForInterfaceMember(member),
                        symbol
                    )
                )
                {
                    yield return member;
                }
            }
        }
    }
}
