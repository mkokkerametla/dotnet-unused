using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotnetUnused.Core;

/// <summary>
/// Responsible for loading .sln or .csproj files using MSBuildWorkspace
/// </summary>
public sealed class SolutionLoader
{
    private static bool _msbuildRegistered;

    public static void EnsureMSBuildRegistered()
    {
        if (!_msbuildRegistered)
        {
            // Query for available MSBuild instances
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();

            // Prefer the latest .NET SDK
            var instance = instances.OrderByDescending(i => i.Version).FirstOrDefault();

            if (instance != null)
            {
                MSBuildLocator.RegisterInstance(instance);
            }
            else
            {
                // Fallback to default
                MSBuildLocator.RegisterDefaults();
            }

            _msbuildRegistered = true;
        }
    }

    /// <summary>
    /// Loads a solution or project file
    /// </summary>
    public async Task<Solution> LoadAsync(
        string path,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureMSBuildRegistered();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var workspace = MSBuildWorkspace.Create();

        // Subscribe to workspace failures using the recommended API
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
            {
                progress?.Report($"Warning: {args.Diagnostic.Message}");
            }
            else
            {
                progress?.Report($"Error: {args.Diagnostic.Message}");
            }
        });

        Solution solution;

        if (
            path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
        )
        {
            progress?.Report($"Loading solution: {path}");
            solution = await workspace.OpenSolutionAsync(path);
            progress?.Report($"Loaded solution with {solution.ProjectIds.Count} projects");
        }
        else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report($"Loading project: {path}");
            var project = await workspace.OpenProjectAsync(path);
            solution = project.Solution;
            progress?.Report($"Loaded project: {project.Name}");
        }
        else
        {
            throw new ArgumentException(
                $"Unsupported file type: {path}. Expected .sln, .slnx, or .csproj"
            );
        }

        return solution;
    }

    /// <summary>
    /// Filters out generated files that should be excluded from analysis
    /// </summary>
    public static bool ShouldAnalyzeDocument(Document document) =>
        FileFilter.ShouldAnalyze(document.FilePath);
}
