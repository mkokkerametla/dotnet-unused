namespace DotnetUnused.Models;

/// <summary>
/// Contains the results of the unused code analysis
/// </summary>
public sealed class AnalysisResult
{
    public List<SymbolDefinition> UnusedSymbols { get; } = new();
    public List<UsingDirectiveInfo> UnusedUsings { get; } = new();
    public List<UnusedPackageInfo> UnusedPackages { get; } = new();
    public TimeSpan Duration { get; set; }
    public int TotalSymbolsAnalyzed { get; set; }
    public int TotalReferencesFound { get; set; }
    public int TotalPackagesAnalyzed { get; set; }

    public void AddUnusedSymbol(SymbolDefinition symbol)
    {
        UnusedSymbols.Add(symbol);
    }

    public void AddUnusedUsing(UsingDirectiveInfo usingDirective)
    {
        UnusedUsings.Add(usingDirective);
    }

    public void AddUnusedPackage(UnusedPackageInfo package)
    {
        UnusedPackages.Add(package);
    }
}
