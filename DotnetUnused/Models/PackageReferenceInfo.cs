namespace DotnetUnused.Models;

/// <summary>
/// Represents a NuGet package reference found in a project file.
/// </summary>
public sealed class PackageReferenceInfo
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this reference contributes compile-time assets (i.e. can expose a namespace/API in code).
    /// Packages that only supply build/analyzer/runtime assets (e.g. DotNet.ReproducibleBuilds,
    /// referenced with IncludeAssets excluding "compile" or ExcludeAssets="compile") contribute no
    /// namespace to find, so they must never be reported as unused.
    /// </summary>
    public bool IncludesCompileAssets { get; set; } = true;
}
