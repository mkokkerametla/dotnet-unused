# Changelog

All notable changes to DotnetUnused will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **`.slnx` solution support** - analyze the new XML-based solution format (#7)
- **Markdown report output** via `--format markdown` (alias `md`), written to `--output` (#9)
- **`--ignore-attributes` option** - comma-separated attribute names whose members are never flagged as unused, e.g. `--ignore-attributes TestInitialize,SetUp` (#8)
- Built-in recognition of test lifecycle attributes (MSTest `TestInitialize`/`TestCleanup`/`ClassInitialize`/`ClassCleanup`/`AssemblyInitialize`/`AssemblyCleanup`, NUnit `SetUp`/`TearDown`/`OneTimeSetUp`/`OneTimeTearDown`) so these hooks are no longer reported as unused (#8)
- **.NET 10 support** - the tool now multi-targets `net8.0` and `net10.0`, remaining backward compatible with the .NET 8 runtime

### Changed

- Unused-package detection now honors `IncludeAssets`/`ExcludeAssets` on `PackageReference`: packages that contribute no compile-time assets (e.g. `DotNet.ReproducibleBuilds`) are no longer reported as unused (#9)
- README corrected to use the `DotnetUnused` package id and documents the `dotnet tool exec` usage (#9)

## [1.2.0] - 2025-12-31

### Added

- **CS8019 compiler diagnostic fallback** when IDE analyzers are not loaded
- **Constructed generic type handling** in reference walker (e.g., `List<int>.Add` correctly maps to `List<T>.Add`)
- **VS Code Extension: One-click CLI installation** with automated setup
- **VS Code Extension: Fix Unused Usings command** via Command Palette
- **VS Code Extension: Install/Update CLI Tool command** for easy maintenance
- Configurable CLI check timeout (default: 5000ms)
- Progress feedback before fixing: shows count of unused usings
- Automatic PATH fallback to standard .dotnet tools installation location
- Integration tests for UnusedUsingAnalyzer CS8019 fallback
- Unit tests for constructed generic type handling

### Changed

- **VS Code Extension: Improved CLI detection** - finds CLI even when not in PowerShell PATH
- **VS Code Extension: Better installation flow** - verifies file exists before reporting success
- Regex performance optimization using `[GeneratedRegex]` attribute (10x faster)
- Refactored VS Code extension: extracted `selectProjectOrSolution()` helper (reduced 70 lines duplication)

### Fixed

- **SECURITY: Command injection vulnerability** in VS Code extension (removed unsafe `shell: true`)
- **CRITICAL: Atomic file writes** in UsingDirectiveFixer (prevents corruption on failure/cancellation)
- **CRITICAL: Path filtering false positives** using precise NuGet/SDK folder checks
- **BUG: Namespace extraction** now uses Roslyn syntax parsing (handles "using" in namespace names)
- Flaky progress test replaced with deterministic return value test
- VS Code extension handles missing CLI gracefully with installation prompt
- Installation race condition: 500ms delay before verification
- Package ID case-sensitivity: `dotnetunused` (lowercase)

### Security

- Removed `shell: true` from all VS Code extension spawn calls
- Conditional shell usage: only for non-absolute paths
- Prevents malicious config from executing arbitrary commands

## [1.0.0] - 2025-12-29

### Added

- **Unused using directives detection** (enabled by default using CS8019/IDE0005 diagnostics)
- Identifies unnecessary namespace imports in all C# files
- Support for global usings analysis (.NET 6+ implicit usings)
- Automatic handling of extension methods, LINQ, and attributes via Roslyn
- **`--fix` flag** to automatically remove unused usings from files
- **`--skip-usings` flag** to disable using analysis for faster symbol-only analysis
- Smart filtering: excludes EF Core migrations (timestamp patterns, ModelSnapshot)
- Batch removal of unused usings while preserving file formatting
- External dependency filtering (NuGet packages, SDK files excluded)
- File-level using directive cleanup with preserved indentation and spacing
- JSON output includes unused using locations, namespaces, and messages
- Console report shows unused usings grouped by file with line numbers
- Microsoft.CodeAnalysis.Features 5.0.0 dependency for Roslyn analyzer support

### Changed

- **Extended default analysis** to include using directive validation (can be disabled with --skip-usings)
- Updated report format to include "Unused Using Directives" section
- Console output now shows both unused symbols and unused usings by default
- Summary table includes unused usings count
- FileFilter now excludes Migrations folder for EF Core auto-generated files

### Deprecated

- **JSON API**: `Summary.UnusedCount` field is deprecated in favor of `Summary.UnusedSymbolsCount` for clarity
  - Both fields are currently present for backward compatibility
  - `UnusedCount` will be removed in v2.0.0
  - Consumers should migrate to `UnusedSymbolsCount` which more accurately describes the counted items

### Fixed

- File formatting preservation when using --fix (no more unwanted reformatting)
- Batch removal of all unused usings in one operation (prevents line number conflicts)
- Smart migration detection (excludes EF migrations, allows user files in Migrations folder)

## [0.1.1] - 2025-12-28

### Added

- GitVersion integration for automatic semantic versioning
- Unit test infrastructure with xUnit (85 passing tests)
- FileFilter utility class for shared file filtering logic
- Ctrl+C cancellation support with graceful shutdown
- VS Code extension automated release workflow
- Comprehensive test coverage for FileFilter edge cases
- AnalysisResult model tests
- GitVersion documentation (GitVersion.md)
- VS Code extension publishing documentation (vscode-extension/PUBLISHING.md)
- Test project documentation (DotnetUnused.Tests/README.md)

### Changed

- Improved CLI argument parsing with bool.TryParse
- Refactored SymbolIndexer, ReferenceWalker, and SolutionLoader to use FileFilter
- Enhanced GitHub Actions workflow with GitVersion
- Updated VS Code extension to use terminal output by default
- Synchronized versions between CLI tool and VS Code extension

### Fixed

- Command injection vulnerability in VS Code extension terminal mode
- Temp file cleanup in VS Code extension error paths
- Missing icon reference in VS Code extension
- FileFilter edge cases (whitespace handling, relative paths, exact pattern matching)
- Bool.Parse crash on invalid --exclude-public values

### Security

- Fixed command injection in VS Code extension (replaced string interpolation with ShellExecution)
- Added proper argument escaping for CLI execution

## [1.0.0] - 2025-12-26

### Added

- Initial release of DotnetUnused CLI tool
- Unused method detection
- Unused property detection
- Unused field detection
- Generic method support (correctly handles `Method<T>()` calls)
- Support for .NET Core 2.0+, .NET 5+, and .NET Framework 4.x projects
- Smart heuristics to avoid false positives:
  - Excludes entry points (Main methods)
  - Excludes public API (configurable)
  - Excludes ASP.NET Core controllers and endpoints
  - Excludes test methods (xUnit, NUnit, MSTest)
  - Excludes serialization members
  - Excludes DI constructors
  - Excludes interface/abstract/override/virtual members
  - Excludes members with `[UsedImplicitly]` or `[Preserve]` attributes
- Console output with beautiful tables (Spectre.Console)
- JSON output for CI/CD integration
- Parallel processing for fast analysis
- Single-pass syntax tree traversal
- Cross-project reference support
- Partial class support

### Performance

- Analyzes medium-sized solutions (<500k LOC) in under 10 seconds
- Single compilation per project
- Concurrent symbol indexing

### Compatibility

- Works with SDK-style projects
- Works with legacy .csproj format
- Requires Visual Studio MSBuild for .NET Framework projects

[Unreleased]: https://github.com/kokkerametla/dotnet-unused/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/kokkerametla/dotnet-unused/releases/tag/v1.0.0
