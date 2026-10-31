import * as vscode from "vscode";
import * as child_process from "child_process";
import * as fs from "fs";
import * as path from "path";
import * as os from "os";
import { UnusedCodeReport, AnalysisOptions, ReportFormat } from "./types";

export class CliRunner {
  private outputChannel: vscode.OutputChannel;

  constructor(outputChannel: vscode.OutputChannel) {
    this.outputChannel = outputChannel;
  }

  /**
   * Gets the CLI path, checking standard installation location if not in PATH
   */
  public getCliPath(): string {
    const config = vscode.workspace.getConfiguration("dotnet-unused");
    let cliPath = config.get<string>("cliPath") || "dotnet-unused";

    // If no custom path set, try to find CLI in standard .dotnet tools location
    if (cliPath === "dotnet-unused") {
      const home = process.env.USERPROFILE || process.env.HOME;
      if (home) {
        const standardPath = path.join(
          home,
          ".dotnet",
          "tools",
          "dotnet-unused",
        );
        const windowsPath = standardPath + ".exe";

        // Check if CLI exists at standard location
        if (fs.existsSync(windowsPath)) {
          this.outputChannel.appendLine(`[Debug] Using CLI at: ${windowsPath}`);
          return windowsPath;
        } else if (fs.existsSync(standardPath)) {
          this.outputChannel.appendLine(
            `[Debug] Using CLI at: ${standardPath}`,
          );
          return standardPath;
        }
      }
    }

    return cliPath;
  }

  async runAnalysis(
    options: AnalysisOptions,
  ): Promise<UnusedCodeReport | null> {
    const tempFile = path.join(os.tmpdir(), `dotnet-unused-${Date.now()}.json`);

    try {
      await this.writeReport(options.targetPath, "json", tempFile);
      const content = fs.readFileSync(tempFile, "utf-8");
      return JSON.parse(content) as UnusedCodeReport;
    } catch (error) {
      this.outputChannel.appendLine(`Error: ${error}`);
      throw error;
    } finally {
      // Always cleanup temp file
      if (fs.existsSync(tempFile)) {
        try {
          fs.unlinkSync(tempFile);
        } catch (cleanupError) {
          this.outputChannel.appendLine(
            `Warning: Failed to cleanup temp file: ${cleanupError}`,
          );
        }
      }
    }
  }

  /**
   * Runs an analysis and writes the report to outputPath in the given format
   */
  async exportReport(
    targetPath: string,
    format: ReportFormat,
    outputPath: string,
  ): Promise<void> {
    await this.writeReport(targetPath, format, outputPath);
  }

  private async writeReport(
    targetPath: string,
    format: ReportFormat,
    outputPath: string,
  ): Promise<void> {
    const config = vscode.workspace.getConfiguration("dotnet-unused");
    const excludePublic = config.get<boolean>("excludePublic", true);

    await this.runCli([
      targetPath,
      "--format",
      format,
      "--output",
      outputPath,
      "--exclude-public",
      excludePublic.toString(),
    ]);

    if (!fs.existsSync(outputPath)) {
      throw new Error("Report file not found");
    }
  }

  /**
   * Runs the CLI and resolves when it exits with code 0. No shell is used: with a shell,
   * Node joins the arguments with spaces unquoted, so a path containing a space would be
   * split into several arguments.
   */
  private runCli(args: string[]): Promise<void> {
    const cliPath = this.getCliPath();
    this.outputChannel.appendLine(`Running: ${cliPath} ${args.join(" ")}`);

    return new Promise((resolve, reject) => {
      const process = child_process.spawn(cliPath, args, {
        cwd: vscode.workspace.workspaceFolders?.[0]?.uri.fsPath,
      });

      let stderr = "";

      process.stdout.on("data", (data) => {
        this.outputChannel.append(data.toString());
      });

      process.stderr.on("data", (data) => {
        stderr += data.toString();
        this.outputChannel.append(data.toString());
      });

      process.on("close", (code) => {
        if (code === 0) {
          resolve();
        } else {
          this.outputChannel.appendLine(`Process exited with code ${code}`);
          this.outputChannel.appendLine(`stderr: ${stderr}`);
          reject(new Error(`Analysis failed with code ${code}`));
        }
      });

      process.on("error", (error) => {
        this.outputChannel.appendLine(
          `Failed to start process: ${error.message}`,
        );
        reject(error);
      });
    });
  }

  async checkCliAvailable(): Promise<boolean> {
    const cliPath = this.getCliPath();

    return new Promise((resolve) => {
      // Use spawn without shell for security (prevents command injection)
      // Only use shell if cliPath is not an absolute path (for 'dotnet-unused' command)
      const useShell = !path.isAbsolute(cliPath);
      const childProc = child_process.spawn(cliPath, ["--help"], {
        shell: useShell,
        env: { ...process.env },
      });

      let hasStdout = false;
      let completed = false;

      childProc.stdout.on("data", () => {
        hasStdout = true; // CLI is working if it outputs to stdout
      });

      childProc.on("close", (code: number | null) => {
        if (completed) return;
        completed = true;

        // CLI is available only if it outputted to stdout (not just stderr)
        // and exited with code 0 or 1 (help usually exits with 1)
        const isAvailable = hasStdout && (code === 0 || code === 1);
        this.outputChannel.appendLine(
          `[Debug] CLI check: path="${cliPath}", code=${code}, hasStdout=${hasStdout}, available=${isAvailable}`,
        );
        resolve(isAvailable);
      });

      childProc.on("error", (error: Error) => {
        if (completed) return;
        completed = true;
        this.outputChannel.appendLine(
          `[Debug] CLI check failed: ${error.message}`,
        );
        resolve(false);
      });

      // Configurable timeout (default 5 seconds)
      const config = vscode.workspace.getConfiguration("dotnet-unused");
      const timeout = config.get<number>("cliCheckTimeout", 5000);

      setTimeout(() => {
        if (completed) return;
        completed = true;
        this.outputChannel.appendLine(
          `[Debug] CLI check timed out after ${timeout}ms`,
        );
        childProc.kill();
        resolve(false);
      }, timeout);
    });
  }
}
