# Dotnet Unused

[![NuGet](https://img.shields.io/nuget/v/DotnetUnused.svg)](https://www.nuget.org/packages/DotnetUnused/)
[![VS Code Extension](https://img.shields.io/visual-studio-marketplace/v/kokkerametla.dotnet-unused-vscode)](https://marketplace.visualstudio.com/items?itemName=kokkerametla.dotnet-unused-vscode)

A CLI tool for detecting unused code in .NET solutions using Roslyn static analysis.

## Features

- Detects unused types (classes, structs, records, interfaces, enums, delegates), methods, properties, fields, and using directives
- Detects unused NuGet packages
- Auto-fix support for removing unused usings
- JSON export for CI/CD integration
- MCP server so AI agents (Claude Code, GitHub Copilot, Cursor) can find and clean up unused code
- Works with .NET Core, .NET 5+, and .NET Framework

## Installation

Install as a global .NET tool (package id is `DotnetUnused`, command is `dotnet-unused`):

```bash
dotnet tool install --global DotnetUnused
```

Then run it with the `dotnet-unused` command:

```bash
dotnet-unused MySolution.sln
```

Or run it without installing, using `dotnet tool exec`:

```bash
dotnet tool exec DotnetUnused MySolution.sln --unused-packages
```

Or download standalone binaries from [GitHub Releases](https://github.com/kokkerametla/dotnet-unused/releases).

## Usage

```bash
dotnet-unused MySolution.sln
```

Solutions (`.sln` and `.slnx`) and individual projects (`.csproj`) are all supported.

### Options

| Option                | Description                                                                                              |
| --------------------- | -------------------------------------------------------------------------------------------------------- |
| `--format, -f`        | Output format: `text`, `json`, or `markdown` (default: `text`)                                           |
| `--output, -o`        | Output file path (for `json` or `markdown` format)                                                       |
| `--exclude-public`    | Exclude public members (default: `true`)                                                                 |
| `--skip-usings`       | Skip unused using directives analysis                                                                    |
| `--fix`               | Automatically remove unused usings                                                                       |
| `--unused-packages`   | Detect unused NuGet packages                                                                             |
| `--ignore-attributes` | Comma-separated attribute names whose members are never flagged                                          |
| `--exclude-packages`  | Comma-separated NuGet package IDs that are never flagged as unused (requires `--unused-packages`)        |
| `--verbose, -v`       | Explain why each NuGet package was judged used, unused, or skipped (requires `--unused-packages`)        |
| `--msbuild-path`      | Directory of the MSBuild to use (e.g. a .NET SDK folder). Also settable via `DOTNET_UNUSED_MSBUILD_PATH` |
| `--fail-on-unused`    | Exit with code 2 when any unused code is found, for CI gating (default: always exit 0 on a successful analysis) |
| `--strict`            | Treat project load errors as fatal instead of analyzing a partially loaded solution                       |
| `--include-virtual`   | Also report unused virtual members (default: virtuals are skipped, since polymorphism can hide their uses) |
| `--help, -h`          | Show command help                                                                                        |

### Examples

```bash
# Basic analysis
dotnet-unused MySolution.sln

# Show CLI help
dotnet-unused --help

# Generate JSON report
dotnet-unused MySolution.sln --format json --output report.json

# Generate a Markdown report (great for PRs / sharing)
dotnet-unused MySolution.sln --format markdown --output report.md

# Analyze an .slnx solution
dotnet-unused MySolution.slnx

# Auto-fix unused usings
dotnet-unused MySolution.sln --fix

# Detect unused NuGet packages
dotnet-unused MySolution.sln --unused-packages

# Ignore members marked with specific attributes
dotnet-unused MySolution.sln --ignore-attributes TestInitialize,SetUp

# Never flag specific packages, and show why each package was judged
dotnet-unused MySolution.sln --unused-packages --exclude-packages Humanizer,Polly --verbose

# Analyze single project
dotnet-unused MyProject.csproj

# Fail the build when unused code is found (CI gating)
dotnet-unused MySolution.sln --fail-on-unused
```

## MCP server (AI agents)

`dotnet-unused mcp` runs a [Model Context Protocol](https://modelcontextprotocol.io) server over stdio, so coding agents can call the analyzer as a tool and get structured JSON back instead of parsing console output.

| Tool                | What it does                                                                                                                                                                       |
| ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `analyze_solution`  | Read-only. Reports unused symbols, usings and (optionally) NuGet packages, with file and line for each. Accepts a `.sln`, `.slnx` or `.csproj` path, or a directory containing one |
| `fix_unused_usings` | Removes unused using directives from files on disk, optionally limited to given files. Marked destructive, so clients ask before running it                                        |

A typical session: the agent analyzes the solution, removes the unused usings once you approve, then analyzes again to confirm they are gone. Unused types and members are reported for you to review rather than deleted automatically.

**Claude Code**

```bash
claude mcp add dotnet-unused -- dotnet-unused mcp
```

**VS Code (GitHub Copilot)**, in `.vscode/mcp.json`:

```json
{
  "servers": {
    "dotnet-unused": {
      "type": "stdio",
      "command": "dotnet-unused",
      "args": ["mcp"]
    }
  }
}
```

**Cursor and other clients**, in the client's MCP config (for Cursor, `.cursor/mcp.json`):

```json
{
  "mcpServers": {
    "dotnet-unused": { "command": "dotnet-unused", "args": ["mcp"] }
  }
}
```

To pick the MSBuild used for loading projects, add `"--msbuild-path", "<dir>"` to `args` or set `DOTNET_UNUSED_MSBUILD_PATH`.

## VS Code Extension

Install "Dotnet Unused" from the [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=kokkerametla.dotnet-unused-vscode) for inline diagnostics and one-click fixes.

## Known limitations

- **Reflection and `dynamic`:** members reached only via reflection or dynamic dispatch are invisible to static analysis and may be flagged. Protect them with `--ignore-attributes`, or mark them `[UsedImplicitly]` / `[Preserve]`.
- **Solution scope:** analysis covers the solution or project you pass in. Members used only by code outside it (for example, a library consumed by another solution) will be flagged — keep `--exclude-public` (the default) for libraries.
- **Only `.cs` files are analyzed.** Razor, XAML, and CSHTML bindings are not tracked; XAML event handlers are spared by a name-based heuristic instead, which can also spare a genuinely unused helper with an event-handler-like name.
- **Virtual members** are skipped by default (see `--include-virtual`).
- **Generated files are excluded** (`.g.cs`, `.Designer.cs`, EF Core migrations).
- **Unused packages** are detected by combining namespace usage with the assemblies recorded in the restore assets file. A package used only through MSBuild targets, content files, or other non-code means may still be flagged — review before removing.
- **Interface implementations** count as used when their interface member is referenced anywhere in the solution; an implementation whose interface member is never referenced is still flagged.

## Requirements

- .NET 8.0 SDK or higher (the tool runs on the .NET 8 and .NET 10 runtimes)
- For .NET Framework projects: Visual Studio installation

## License

See LICENSE file.
