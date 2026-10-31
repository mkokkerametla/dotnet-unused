using System.ComponentModel;

namespace DotnetUnused.Mcp;

public sealed record AnalyzeSolutionResult(
    [property: Description("Absolute path of the solution or project that was analyzed")]
        string Path,
    [property: Description("Wall-clock analysis time in seconds, including loading")]
        double DurationSeconds,
    [property: Description(
        "Project load errors. When non-empty, part of the solution was not analyzed and results may be incomplete"
    )]
        IReadOnlyList<string> LoadErrors,
    AnalysisSummary Summary,
    [property: Description(
        "Unused methods, properties, fields and types, ordered by file and line"
    )]
        IReadOnlyList<UnusedSymbolItem> UnusedSymbols,
    [property: Description("Unused using directives, ordered by file and line")]
        IReadOnlyList<UnusedUsingItem> UnusedUsings,
    [property: Description("NuGet packages with no detected usage")]
        IReadOnlyList<UnusedPackageItem> UnusedPackages,
    [property: Description(
        "True when a list was cut to maxResults; the summary still has the full counts"
    )]
        bool Truncated
);

public sealed record AnalysisSummary(
    int SymbolsAnalyzed,
    int UnusedSymbols,
    [property: Description(
        "Unused symbol counts keyed by kind (Method, Property, Field, Class, ...)"
    )]
        IReadOnlyDictionary<string, int> UnusedSymbolsByKind,
    [property: Description("Null when unused usings were not analyzed")] int? UnusedUsings,
    [property: Description("Null when packages were not analyzed")] int? PackagesAnalyzed,
    [property: Description("Null when packages were not analyzed")] int? UnusedPackages
);

public sealed record UnusedSymbolItem(
    [property: Description(
        "Method, Property, Field, Class, Struct, Record, Interface, Enum or Delegate"
    )]
        string Kind,
    string Name,
    string FullyQualifiedName,
    [property: Description("Path relative to the directory of the analyzed solution or project")]
        string File,
    int Line
);

public sealed record UnusedUsingItem(
    string Namespace,
    [property: Description("Path relative to the directory of the analyzed solution or project")]
        string File,
    int Line
);

public sealed record UnusedPackageItem(
    string PackageId,
    string Version,
    string Project,
    string ProjectPath
);

public sealed record FixUnusedUsingsResult(
    [property: Description("Absolute path of the solution or project that was fixed")] string Path,
    [property: Description("Number of using directives removed")] int UsingsRemoved,
    [property: Description("Files that were rewritten, relative to the solution directory")]
        IReadOnlyList<string> ModifiedFiles,
    [property: Description(
        "Requested files that are not part of the solution or project, so nothing was done to them"
    )]
        IReadOnlyList<string> UnmatchedFiles,
    [property: Description(
        "Project load errors. When non-empty, part of the solution was not loaded, so some files may not have been fixed"
    )]
        IReadOnlyList<string> LoadErrors
);
