# Dotnet Unused Code Analyzer - VS Code Extension

Detect and highlight unused code in .NET solutions directly in Visual Studio Code using Roslyn static analysis.

## Features

- **✨ One-Click CLI Installation** (New in v1.2.0): Automatically installs CLI tool if missing
- **🔧 Auto-Fix Unused Usings** (New in v1.2.0): Remove all unused using directives with one command
- **🎯 Smart PATH Detection** (New in v1.2.0): Finds CLI even when not in PowerShell PATH
- **On-Demand Analysis**: Analyze your entire workspace or just the current file
- **Inline Diagnostics**: See unused code warnings directly in your editor with squiggly underlines
- **Unused Usings Detection**: Find unnecessary using directives (enabled by default in v1.0.0+)
- **Problems Panel**: All unused symbols and usings listed in VS Code's Problems panel
- **Tree View**: Sidebar panel showing unused symbols and usings organized by type
- **Configurable Severity**: Set diagnostic severity (Error, Warning, Information, Hint)
- **Optional Analysis**: Skip usings analysis with configuration setting
- **Jump to Definition**: Click any unused symbol in the tree view to navigate to its location
- **Export Reports**: Generate JSON reports of unused code (coming soon)

## Requirements

This extension requires the `dotnet-unused` CLI tool. **Don't worry** - the extension will offer to install it automatically if it's not found!

**Manual installation:**

```bash
dotnet tool install --global dotnetunused
```

**Note**: Package ID is `dotnetunused` (lowercase), command is `dotnet-unused` (with hyphen).

See the [CLI installation guide](https://github.com/kokkerametla/dotnet-unused#installation) for more options.

## Usage

### Commands

Access commands via Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`):

- **Dotnet Unused: Analyze Workspace for Unused Code** - Analyze entire solution/project
- **Dotnet Unused: Analyze Current File for Unused Code** - Analyze only the current C# file's project
- **Dotnet Unused: Fix Unused Using Directives** ✨ (New in v1.2.0) - Auto-remove unused usings
- **Dotnet Unused: Install/Update CLI Tool** ✨ (New in v1.2.0) - Install or update the CLI
- **Dotnet Unused: Clear Unused Code Diagnostics** - Clear all diagnostics from the editor
- **Dotnet Unused: Export Unused Code Report (JSON)** - Export analysis results (coming soon)

### Tree View

The "Dotnet Unused" sidebar panel shows:

- Unused symbols grouped by type (Methods, Properties, Fields)
- File location and line number for each symbol
- Click any item to jump to its definition

### Context Menu

Right-click on `.sln` or `.csproj` files in the Explorer to analyze them directly.

## Extension Settings

Configure the extension in VS Code settings:

- `dotnet-unused.cliPath`: Path to dotnet-unused CLI tool (leave empty for global installation)
- `dotnet-unused.excludePublic`: Exclude public members from detection (default: `true`)
- `dotnet-unused.diagnosticSeverity`: Severity level for diagnostics (`Error`, `Warning`, `Information`, `Hint`)
- `dotnet-unused.useTerminal`: Show output in integrated terminal instead of output window (default: `true`)
- `dotnet-unused.autoRunOnSave`: Automatically analyze when C# files are saved (default: `false`)
- `dotnet-unused.cliCheckTimeout`: Timeout in milliseconds for CLI check (default: `5000`) ✨ New in v1.2.0

## How It Works

The extension integrates with the `dotnet-unused` CLI tool:

1. Runs analysis on your .NET solution or project
2. Parses the JSON output from the CLI
3. Creates VS Code diagnostics for unused symbols
4. Updates the tree view with results
5. Allows navigation to unused code locations

## Supported Project Types

- .NET Core 2.0+
- .NET 5+
- .NET 6/7/8/9+
- .NET Framework 4.x (requires Visual Studio MSBuild)
- SDK-style projects
- Legacy .csproj format

## Known Limitations

- Static analysis only - does not detect runtime or reflection-based usage
- Conservative approach may miss some unused code to avoid false positives
- No incremental analysis - full solution analysis on each run

## Troubleshooting

### "dotnet-unused CLI tool not found"

**✨ Automatic Installation (v1.2.0+):**
The extension will offer to install the CLI automatically. Just click "Install Automatically" when prompted!

**Manual Installation:**

```bash
dotnet tool install --global dotnetunused
```

Or use the Command Palette:

- `Ctrl+Shift+P` → "Dotnet Unused: Install/Update CLI Tool"

**Custom Path:**

```json
{
  "dotnet-unused.cliPath": "C:\\path\\to\\dotnet-unused.exe"
}
```

**Note:** The extension automatically finds the CLI at `%USERPROFILE%\.dotnet\tools\` on Windows or `~/.dotnet/tools/` on Unix, even if not in PATH!

### Analysis is slow

- Use "Analyze Current File" for faster feedback
- Disable `autoRunOnSave` for large solutions
- Consider analyzing only specific projects instead of entire solutions

## Contributing

Issues and contributions welcome at [GitHub](https://github.com/kokkerametla/dotnet-unused).

## License

MIT - See LICENSE file for details.
