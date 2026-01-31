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
}
