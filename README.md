# Dotnet Unused

[![NuGet](https://img.shields.io/nuget/v/DotnetUnused.svg)](https://www.nuget.org/packages/DotnetUnused/)
[![VS Code Extension](https://img.shields.io/visual-studio-marketplace/v/kokkerametla.dotnet-unused-vscode)](https://marketplace.visualstudio.com/items?itemName=kokkerametla.dotnet-unused-vscode)

A CLI tool for detecting unused code in .NET solutions using Roslyn static analysis.

## Features

- Detects unused methods, properties, fields, and using directives
- Detects unused NuGet packages
- Auto-fix support for removing unused usings
- JSON export for CI/CD integration
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

| Option                | Description                                                     |
| --------------------- | --------------------------------------------------------------- |
| `--format, -f`        | Output format: `text`, `json`, or `markdown` (default: `text`)  |
| `--output, -o`        | Output file path (for `json` or `markdown` format)              |
| `--exclude-public`    | Exclude public members (default: `true`)                        |
| `--skip-usings`       | Skip unused using directives analysis                           |
| `--fix`               | Automatically remove unused usings                              |
| `--unused-packages`   | Detect unused NuGet packages                                    |
| `--ignore-attributes` | Comma-separated attribute names whose members are never flagged |
| `--help, -h`          | Show command help                                               |

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

# Analyze single project
dotnet-unused MyProject.csproj
```

## VS Code Extension

Install "Dotnet Unused" from the [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=kokkerametla.dotnet-unused-vscode) for inline diagnostics and one-click fixes.

## Requirements

- .NET 8.0 SDK or higher (the tool runs on the .NET 8 and .NET 10 runtimes)
- For .NET Framework projects: Visual Studio installation

## License

See LICENSE file.
