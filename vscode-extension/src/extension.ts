import * as vscode from "vscode";
import * as path from "path";
import * as fs from "fs";
import { CliRunner } from "./cliRunner";
import { DiagnosticProvider } from "./diagnosticProvider";
import { UnusedSymbolsProvider } from "./unusedSymbolsProvider";
import { UnusedCodeReport } from "./types";

let outputChannel: vscode.OutputChannel;
let cliRunner: CliRunner;
let diagnosticProvider: DiagnosticProvider;
let unusedSymbolsProvider: UnusedSymbolsProvider;

export function activate(context: vscode.ExtensionContext) {
  outputChannel = vscode.window.createOutputChannel("Dotnet Unused");
  cliRunner = new CliRunner(outputChannel);
  diagnosticProvider = new DiagnosticProvider();
  unusedSymbolsProvider = new UnusedSymbolsProvider();

  vscode.window.registerTreeDataProvider(
    "dotnet-unused.unusedSymbols",
    unusedSymbolsProvider,
  );

  checkCliInstallation();

  context.subscriptions.push(
    vscode.commands.registerCommand(
      "dotnet-unused.analyzeWorkspace",
      analyzeWorkspace,
    ),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand(
      "dotnet-unused.analyzeCurrentFile",
      analyzeCurrentFile,
    ),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("dotnet-unused.clearDiagnostics", () => {
      diagnosticProvider.clear();
      unusedSymbolsProvider.refresh(null);
      vscode.window.showInformationMessage("Cleared unused code diagnostics");
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("dotnet-unused.exportReport", exportReport),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand(
      "dotnet-unused.fixUnusedUsings",
      fixUnusedUsings,
    ),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("dotnet-unused.installCli", installCli),
  );

  const config = vscode.workspace.getConfiguration("dotnet-unused");
  if (config.get<boolean>("autoRunOnSave")) {
    context.subscriptions.push(
      vscode.workspace.onDidSaveTextDocument((document) => {
        if (document.languageId === "csharp") {
          analyzeCurrentFile();
        }
      }),
    );
  }

  context.subscriptions.push(diagnosticProvider);
  context.subscriptions.push(outputChannel);
}

async function checkCliInstallation() {
  const available = await cliRunner.checkCliAvailable();
  if (!available) {
    const action = await vscode.window.showWarningMessage(
      "dotnet-unused CLI tool not found. Install it to use this extension.",
      "Install Automatically",
      "Manual Instructions",
      "Remind Me Later",
    );

    if (action === "Install Automatically") {
      const installed = await vscode.commands.executeCommand(
        "dotnet-unused.installCli",
      );
      if (installed) {
        // Offer to reload window after successful installation
        const reload = await vscode.window.showInformationMessage(
          "CLI installed! Reload VS Code to use it.",
          "Reload Now",
        );
        if (reload === "Reload Now") {
          vscode.commands.executeCommand("workbench.action.reloadWindow");
        }
      }
    } else if (action === "Manual Instructions") {
      vscode.env.openExternal(
        vscode.Uri.parse(
          "https://github.com/kokkerametla/dotnet-unused#installation",
        ),
      );
    }
  }
}

async function analyzeWorkspace() {
  const targetPath = await selectProjectOrSolution("analyze");
  if (!targetPath) {
    return;
  }

  await runAnalysis(targetPath, "workspace");
}

async function analyzeCurrentFile() {
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document.languageId !== "csharp") {
    vscode.window.showErrorMessage("No active C# file");
    return;
  }

  const csprojPath = await findProjectFile(editor.document.uri.fsPath);
  if (!csprojPath) {
    vscode.window.showErrorMessage(
      "Could not find .csproj file for current file",
    );
    return;
  }

  await runAnalysis(csprojPath, "file");
}

async function runAnalysis(targetPath: string, scope: "workspace" | "file") {
  const config = vscode.workspace.getConfiguration("dotnet-unused");
  const useTerminal = config.get<boolean>("useTerminal", true);

  if (useTerminal) {
    await runAnalysisInTerminal(targetPath, scope);
  } else {
    await runAnalysisInOutputWindow(targetPath, scope);
  }
}

async function runAnalysisInTerminal(
  targetPath: string,
  scope: "workspace" | "file",
) {
  // Check if CLI is available first
  const available = await cliRunner.checkCliAvailable();
  if (!available) {
    const action = await vscode.window.showErrorMessage(
      "dotnet-unused CLI tool not found. Install it to use this feature.",
      "Install Automatically",
      "Manual Instructions",
    );

    if (action === "Install Automatically") {
      await vscode.commands.executeCommand("dotnet-unused.installCli");
    } else if (action === "Manual Instructions") {
      vscode.env.openExternal(
        vscode.Uri.parse(
          "https://github.com/kokkerametla/dotnet-unused#installation",
        ),
      );
    }
    return;
  }

  const cliPath = cliRunner.getCliPath();
  const config = vscode.workspace.getConfiguration("dotnet-unused");
  const excludePublic = config.get<boolean>("excludePublic", true);

  // Log the CLI path being used for debugging
  outputChannel.appendLine(`[Debug] Using CLI path: ${cliPath}`);

  // Use array for arguments - ShellExecution handles escaping automatically
  // This safely handles paths with spaces, quotes, and special characters
  const args: string[] = [targetPath];
  if (excludePublic) {
    args.push("--exclude-public", "true");
  } else {
    args.push("--exclude-public", "false");
  }

  const execution = new vscode.ShellExecution(cliPath, args);
  const task = new vscode.Task(
    { type: "shell", task: "Dotnet Unused Analysis" },
    vscode.TaskScope.Workspace,
    "Dotnet Unused Analysis",
    "dotnet-unused",
    execution,
  );

  await vscode.tasks.executeTask(task);
  vscode.window.showInformationMessage(`Running analysis in terminal...`);
}

async function runAnalysisInOutputWindow(
  targetPath: string,
  scope: "workspace" | "file",
) {
  outputChannel.show();
  outputChannel.appendLine(`\n=== Starting ${scope} analysis ===`);
  outputChannel.appendLine(`Target: ${targetPath}\n`);

  const config = vscode.workspace.getConfiguration("dotnet-unused");
  const excludePublic = config.get<boolean>("excludePublic", true);

  await vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Notification,
      title: `Analyzing ${scope === "workspace" ? "workspace" : "current file"} for unused code...`,
      cancellable: false,
    },
    async () => {
      try {
        const report = await cliRunner.runAnalysis({
          scope,
          excludePublic,
          targetPath,
        });

        if (report) {
          diagnosticProvider.updateDiagnostics(report);
          unusedSymbolsProvider.refresh(report);

          const message = `Found ${report.unusedSymbols.length} unused symbol(s) in ${report.summary.durationSeconds.toFixed(2)}s`;
          outputChannel.appendLine(`\n${message}`);
          vscode.window.showInformationMessage(message);
        }
      } catch (error) {
        const errorMessage =
          error instanceof Error ? error.message : String(error);
        outputChannel.appendLine(`Error: ${errorMessage}`);
        vscode.window.showErrorMessage(`Analysis failed: ${errorMessage}`);
      }
    },
  );
}

/**
 * Helper function to select a .sln or .csproj file from the workspace
 * Reduces code duplication across commands
 */
async function selectProjectOrSolution(action: string): Promise<string | null> {
  const workspaceFolders = vscode.workspace.workspaceFolders;
  if (!workspaceFolders) {
    vscode.window.showErrorMessage("No workspace folder open");
    return null;
  }

  const slnFiles = await vscode.workspace.findFiles(
    "**/*.sln",
    "**/node_modules/**",
    10,
  );

  if (slnFiles.length === 0) {
    const csprojFiles = await vscode.workspace.findFiles(
      "**/*.csproj",
      "**/node_modules/**",
      10,
    );
    if (csprojFiles.length === 0) {
      vscode.window.showErrorMessage(
        "No .sln or .csproj files found in workspace",
      );
      return null;
    }

    if (csprojFiles.length === 1) {
      return csprojFiles[0].fsPath;
    } else {
      const selected = await vscode.window.showQuickPick(
        csprojFiles.map((f) => ({
          label: path.basename(f.fsPath),
          path: f.fsPath,
        })),
        { placeHolder: `Select a .csproj file to ${action}` },
      );
      return selected?.path ?? null;
    }
  } else {
    if (slnFiles.length === 1) {
      return slnFiles[0].fsPath;
    } else {
      const selected = await vscode.window.showQuickPick(
        slnFiles.map((f) => ({
          label: path.basename(f.fsPath),
          path: f.fsPath,
        })),
        { placeHolder: `Select a .sln file to ${action}` },
      );
      return selected?.path ?? null;
    }
  }
}

async function findProjectFile(filePath: string): Promise<string | null> {
  let dir = path.dirname(filePath);
  const root = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(filePath))
    ?.uri.fsPath;

  while (dir && dir !== root) {
    const files = fs.readdirSync(dir);
    const csproj = files.find((f) => f.endsWith(".csproj"));
    if (csproj) {
      return path.join(dir, csproj);
    }
    const parentDir = path.dirname(dir);
    if (parentDir === dir) {
      break;
    }
    dir = parentDir;
  }

  return null;
}

async function exportReport() {
  vscode.window.showInformationMessage(
    "Export report functionality coming soon!",
  );
}

async function installCli(): Promise<boolean> {
  const action = await vscode.window.showInformationMessage(
    "This will install or update the dotnet-unused CLI tool globally.",
    "Install",
    "Cancel",
  );

  if (action !== "Install") {
    return false;
  }

  return await vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Notification,
      title: "Installing dotnet-unused CLI tool...",
      cancellable: false,
    },
    async (progress) => {
      return new Promise<boolean>((resolve) => {
        const child_process = require("child_process");

        progress.report({ message: "Running dotnet tool install..." });

        const process = child_process.spawn(
          "dotnet",
          ["tool", "install", "--global", "dotnetunused"],
          // No shell: true - safer and works fine for dotnet command
        );

        let output = "";
        let errorOutput = "";
        let hasWarnings = false;

        process.stdout.on("data", (data: Buffer) => {
          const text = data.toString();
          output += text;
          outputChannel.appendLine(text);
        });

        process.stderr.on("data", (data: Buffer) => {
          const text = data.toString();
          errorOutput += text;
          outputChannel.appendLine(`[stderr] ${text}`);
          // Track if there are warnings but not errors
          if (text.toLowerCase().includes("warning")) {
            hasWarnings = true;
          }
        });

        process.on("close", async (code: number) => {
          // Wait a moment for file system to settle
          await new Promise((r) => setTimeout(r, 500));

          // Verify installation by checking if CLI file exists
          const home = child_process.env.USERPROFILE || child_process.env.HOME;
          const windowsPath = home
            ? path.join(home, ".dotnet", "tools", "dotnet-unused.exe")
            : null;
          const unixPath = home
            ? path.join(home, ".dotnet", "tools", "dotnet-unused")
            : null;

          const fs = require("fs");
          const isInstalled =
            (windowsPath && fs.existsSync(windowsPath)) ||
            (unixPath && fs.existsSync(unixPath));

          if (code === 0 && isInstalled) {
            const successMessage = hasWarnings
              ? "CLI tool installed with warnings. Check output panel for details. Reload VS Code to use it."
              : "CLI tool installed successfully! Reload VS Code to use it.";

            vscode.window
              .showInformationMessage(
                successMessage,
                "Reload Window",
                hasWarnings ? "View Output" : (undefined as any),
              )
              .then((choice) => {
                if (choice === "Reload Window") {
                  vscode.commands.executeCommand(
                    "workbench.action.reloadWindow",
                  );
                } else if (choice === "View Output") {
                  outputChannel.show();
                }
              });
            resolve(true);
          } else if (
            errorOutput.includes("already installed") ||
            output.includes("already installed")
          ) {
            // Try update instead
            progress.report({
              message: "CLI already exists, trying to update...",
            });
            const updateProcess = child_process.spawn(
              "dotnet",
              ["tool", "update", "--global", "dotnetunused"],
              // No shell: true - safer and works fine for dotnet command
            );

            let updateOutput = "";
            let updateError = "";

            updateProcess.stdout.on("data", (data: Buffer) => {
              const text = data.toString();
              updateOutput += text;
              outputChannel.appendLine(text);
            });

            updateProcess.stderr.on("data", (data: Buffer) => {
              const text = data.toString();
              updateError += text;
              outputChannel.appendLine(`[stderr] ${text}`);
            });

            updateProcess.on("close", async (updateCode: number) => {
              // Wait for file system
              await new Promise((r) => setTimeout(r, 500));

              // Verify update succeeded
              const stillInstalled =
                (windowsPath && fs.existsSync(windowsPath)) ||
                (unixPath && fs.existsSync(unixPath));

              if (updateCode === 0 && stillInstalled) {
                vscode.window
                  .showInformationMessage(
                    "CLI tool updated successfully! Reload VS Code to use it.",
                    "Reload Window",
                  )
                  .then((choice) => {
                    if (choice === "Reload Window") {
                      vscode.commands.executeCommand(
                        "workbench.action.reloadWindow",
                      );
                    }
                  });
                resolve(true);
              } else if (!stillInstalled) {
                vscode.window
                  .showErrorMessage(
                    "Update completed but CLI executable not found. Try reloading VS Code or reinstalling.",
                    "View Output",
                  )
                  .then((choice) => {
                    if (choice === "View Output") {
                      outputChannel.show();
                    }
                  });
                resolve(false);
              } else {
                const errorDetail = updateError.trim() || "Unknown error";
                vscode.window
                  .showErrorMessage(
                    `Failed to update CLI tool (exit code: ${updateCode}): ${errorDetail}`,
                    "View Output",
                  )
                  .then((choice) => {
                    if (choice === "View Output") {
                      outputChannel.show();
                    }
                  });
                resolve(false);
              }
            });
          } else if (!isInstalled) {
            vscode.window
              .showErrorMessage(
                `Installation reported success but CLI executable not found. Try manually: dotnet tool install --global dotnetunused`,
                "View Output",
              )
              .then((choice) => {
                if (choice === "View Output") {
                  outputChannel.show();
                }
              });
            resolve(false);
          } else {
            // Show specific error with stderr if available
            const errorDetail = errorOutput.trim() || "Unknown error";
            vscode.window
              .showErrorMessage(
                `Failed to install CLI tool (exit code: ${code}): ${errorDetail}`,
                "View Output",
              )
              .then((choice) => {
                if (choice === "View Output") {
                  outputChannel.show();
                }
              });
            resolve(false);
          }
        });

        process.on("error", (error: Error) => {
          vscode.window.showErrorMessage(
            `Installation failed: ${error.message}`,
          );
          outputChannel.appendLine(`Error: ${error.message}`);
          resolve(false);
        });
      });
    },
  );
}

async function fixUnusedUsings() {
  // Check if CLI is available first
  const available = await cliRunner.checkCliAvailable();
  if (!available) {
    const action = await vscode.window.showErrorMessage(
      "dotnet-unused CLI tool not found. Install it to use this feature.",
      "Install Automatically",
      "Manual Instructions",
    );

    if (action === "Install Automatically") {
      await vscode.commands.executeCommand("dotnet-unused.installCli");
    } else if (action === "Manual Instructions") {
      vscode.env.openExternal(
        vscode.Uri.parse(
          "https://github.com/kokkerametla/dotnet-unused#installation",
        ),
      );
    }
    return;
  }

  const targetPath = await selectProjectOrSolution("fix");
  if (!targetPath) {
    return;
  }

  // First, run analysis to show user what will be fixed
  let unusedUsingsCount = 0;
  await vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Notification,
      title: "Analyzing unused using directives...",
      cancellable: false,
    },
    async () => {
      try {
        const report = await cliRunner.runAnalysis({
          scope: "workspace",
          excludePublic: false, // Check all usings regardless
          targetPath,
        });
        unusedUsingsCount = report?.unusedUsings?.length ?? 0;
      } catch (error) {
        // If analysis fails, we'll still offer to fix
        outputChannel.appendLine(`Warning: Could not pre-analyze: ${error}`);
      }
    },
  );

  // Confirm before making changes with actual count
  const message =
    unusedUsingsCount > 0
      ? `Found ${unusedUsingsCount} unused using directive(s). Remove them automatically?`
      : "Analyze and remove unused using directives from your files?";

  const confirm = await vscode.window.showWarningMessage(
    message,
    { modal: true },
    "Yes",
    "No",
  );

  if (confirm !== "Yes") {
    return;
  }

  const cliPath = cliRunner.getCliPath();

  // Log the CLI path being used for debugging
  outputChannel.appendLine(`[Debug] Fix command using CLI path: ${cliPath}`);

  // Use array for arguments - ShellExecution handles escaping automatically
  const args: string[] = [targetPath, "--fix"];

  const execution = new vscode.ShellExecution(cliPath, args);
  const task = new vscode.Task(
    { type: "shell", task: "Fix Unused Usings" },
    vscode.TaskScope.Workspace,
    "Fix Unused Using Directives",
    "dotnet-unused",
    execution,
  );

  await vscode.tasks.executeTask(task);
  vscode.window.showInformationMessage("Removing unused using directives...");
}

export function deactivate() {
  if (diagnosticProvider) {
    diagnosticProvider.dispose();
  }
}
