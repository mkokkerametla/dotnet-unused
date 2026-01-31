# Change Log

## [Unreleased]

## [1.0.0] - 2025-12-29

### Added

- Unused using directives detection (enabled by default)
- `dotnet-unused.skipUsings` configuration option to disable (default: false)
- Inline diagnostics for unused using statements with hint severity
- Separate tree view section for unused usings
- Code action to remove unused using
- Code action to remove all unused usings in file

## [0.1.1] - 2025-12-28

### Fixed

- Command injection vulnerability in terminal mode (use ShellExecution with proper argument arrays)
- Temp file cleanup in all error paths with finally blocks
- Missing icon reference in sidebar (use built-in VS Code search icon)

### Changed

- Enhanced configuration description for useTerminal setting
- Improved security with proper argument escaping

### Added

- Automated release workflow via GitHub Actions
- Automatic version synchronization with CLI tool using GitVersion
- PUBLISHING.md documentation

## [0.1.0] - 2024-12-26

### Added

- Initial release of Dotnet Unused Code Analyzer extension
- On-demand analysis for workspace and current file
- Inline diagnostics with configurable severity
- Tree view sidebar for browsing unused symbols
- Integration with dotnet-unused CLI tool
- Problems panel integration
- Terminal output support (show analysis results in integrated terminal)
- Configurable settings (exclude public, diagnostic severity, terminal/output window)
- Jump to definition from tree view
- Context menu for .sln and .csproj files
