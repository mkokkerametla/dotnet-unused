namespace DotnetUnused.Models;

/// <summary>
/// Represents a NuGet package that appears to be unused in a project.
/// </summary>
public sealed class UnusedPackageInfo
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public List<string> CandidateNamespaces { get; set; } = new();
}
