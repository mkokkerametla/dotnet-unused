using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotnetUnused.Core;

/// <summary>
/// Responsible for loading .sln or .csproj files using MSBuildWorkspace
/// </summary>
public sealed class SolutionLoader
{
    /// <summary>
    /// Environment variable that overrides MSBuild discovery (same effect as --msbuild-path)
    /// </summary>
    public const string MSBuildPathEnvironmentVariable = "DOTNET_UNUSED_MSBUILD_PATH";

    private static bool _msbuildRegistered;
    private static string? _registeredDescription;

    private readonly string? _msbuildPath;

    public SolutionLoader(string? msbuildPath = null)
    {
        _msbuildPath = msbuildPath;
    }

    /// <summary>
    /// Picks the MSBuild instance to use. .NET SDK instances win over Visual Studio ones because
    /// the tool runs on modern .NET and a newer Visual Studio MSBuild (.NET Framework) can be
    /// incompatible with the bundled Roslyn BuildHost. Within a kind, the newest version wins.
    /// </summary>
    public static T? SelectInstance<T>(
        IEnumerable<T> instances,
        Func<T, bool> isDotNetSdk,
        Func<T, Version> getVersion
    )
        where T : class
    {
        var all = instances.ToList();
        var sdks = all.Where(isDotNetSdk).ToList();
        var candidates = sdks.Count > 0 ? sdks : all;
        return candidates.OrderByDescending(getVersion).FirstOrDefault();
    }

    /// <summary>
    /// Registers MSBuild once per process. An explicit path (argument or environment variable)
    /// takes precedence over discovery. Returns a description of what was registered.
    /// </summary>
    public static string EnsureMSBuildRegistered(string? msbuildPath = null)
    {
        if (_msbuildRegistered)
        {
            return _registeredDescription!;
        }

        var overridePath = !string.IsNullOrWhiteSpace(msbuildPath)
            ? msbuildPath
            : Environment.GetEnvironmentVariable(MSBuildPathEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!Directory.Exists(overridePath))
            {
                throw new DirectoryNotFoundException(
                    $"MSBuild path not found: {overridePath}. "
                        + "It should be the directory containing MSBuild.dll, "
                        + "e.g. /usr/share/dotnet/sdk/<version>"
                );
            }

            MSBuildLocator.RegisterMSBuildPath(overridePath);
            _registeredDescription = $"MSBuild from {overridePath} (user override)";
        }
        else
        {
            var instance = SelectInstance(
                MSBuildLocator.QueryVisualStudioInstances(),
                i => i.DiscoveryType == DiscoveryType.DotNetSdk,
                i => i.Version
            );

            if (instance != null)
            {
                MSBuildLocator.RegisterInstance(instance);
                _registeredDescription =
                    $"MSBuild {instance.Version} ({instance.Name}) at {instance.MSBuildPath}";
            }
            else
            {
                MSBuildLocator.RegisterDefaults();
                _registeredDescription = "default MSBuild";
            }
        }

        _msbuildRegistered = true;
        return _registeredDescription;
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
        var msbuild = EnsureMSBuildRegistered(_msbuildPath);
        progress?.Report($"Using {msbuild}");

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

        try
        {
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
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or ArgumentException))
        {
            throw new InvalidOperationException(
                $"Failed to load '{path}' using {msbuild}. "
                    + "If this is an MSBuild/BuildHost error, try selecting a different MSBuild "
                    + "with --msbuild-path <dir> (e.g. a .NET SDK directory) or by setting "
                    + $"{MSBuildPathEnvironmentVariable}. Original error: {ex.Message}",
                ex
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
