using System.Text.Json;

namespace DotnetUnused.Core;

/// <summary>
/// Reads NuGet's <c>obj/project.assets.json</c> to map package IDs to the
/// assemblies they contribute at compile time. This grounds unused-package
/// detection in real restore data instead of guessing namespaces from
/// package IDs (which fails for packages whose namespaces don't follow
/// their ID, e.g. a package exposing types under an unrelated root
/// namespace). Returns an empty map when the assets file is missing or
/// unreadable; callers fall back to the namespace heuristic in that case.
/// </summary>
public static class AssetsFileReader
{
    /// <summary>
    /// Maps package ID (case-insensitive) to the set of compile assembly
    /// names (without extension) that package contributes to this project.
    /// </summary>
    public static Dictionary<string, HashSet<string>> ReadCompileAssemblies(string projectDirectory)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(assetsPath));
            if (!doc.RootElement.TryGetProperty("targets", out var targets))
            {
                return result;
            }

            foreach (var target in targets.EnumerateObject())
            {
                foreach (var library in target.Value.EnumerateObject())
                {
                    // Library keys look like "PackageId/Version".
                    var slash = library.Name.IndexOf('/');
                    if (slash <= 0)
                    {
                        continue;
                    }

                    // Skip non-package libraries (project references) when the
                    // type is recorded; when absent, include defensively, since
                    // only package IDs are ever looked up against this map.
                    if (
                        library.Value.TryGetProperty("type", out var type)
                        && type.GetString() != "package"
                    )
                    {
                        continue;
                    }

                    if (!library.Value.TryGetProperty("compile", out var compile))
                    {
                        continue;
                    }

                    var packageId = library.Name[..slash];
                    foreach (var asset in compile.EnumerateObject())
                    {
                        // Asset keys look like "lib/net10.0/Foo.dll".
                        var assemblyName = Path.GetFileNameWithoutExtension(
                            asset.Name.Replace('\\', '/')
                        );
                        if (string.IsNullOrEmpty(assemblyName))
                        {
                            continue;
                        }

                        if (!result.TryGetValue(packageId, out var assemblies))
                        {
                            assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            result[packageId] = assemblies;
                        }
                        assemblies.Add(assemblyName);
                    }
                }
            }
        }
        catch
        {
            // An unreadable assets file must not break analysis; the
            // namespace heuristic still applies.
            return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }
}
