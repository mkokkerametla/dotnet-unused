import * as vscode from "vscode";
import { UnusedCodeReport, UnusedSymbol } from "./types";

export class DiagnosticProvider {
  private diagnosticCollection: vscode.DiagnosticCollection;

  constructor() {
    this.diagnosticCollection =
      vscode.languages.createDiagnosticCollection("dotnet-unused");
  }

  updateDiagnostics(report: UnusedCodeReport) {
    this.diagnosticCollection.clear();

    const config = vscode.workspace.getConfiguration("dotnet-unused");
    const severityConfig = config.get<string>("diagnosticSeverity", "Warning");
    const severity = this.getSeverity(severityConfig);

    const diagnosticsByFile = new Map<string, vscode.Diagnostic[]>();

    for (const symbol of report.unusedSymbols) {
      const fileUri = vscode.Uri.file(symbol.filePath);

      if (!diagnosticsByFile.has(symbol.filePath)) {
        diagnosticsByFile.set(symbol.filePath, []);
      }

      const diagnostic = new vscode.Diagnostic(
        new vscode.Range(symbol.lineNumber - 1, 0, symbol.lineNumber - 1, 1000),
        this.createMessage(symbol),
        severity,
      );

      diagnostic.source = "dotnet-unused";
      diagnostic.code = `unused-${symbol.kind.toLowerCase()}`;

      diagnosticsByFile.get(symbol.filePath)!.push(diagnostic);
    }

    for (const [filePath, diagnostics] of diagnosticsByFile) {
      this.diagnosticCollection.set(vscode.Uri.file(filePath), diagnostics);
    }
  }

  clear() {
    this.diagnosticCollection.clear();
  }

  dispose() {
    this.diagnosticCollection.dispose();
  }

  private getSeverity(config: string): vscode.DiagnosticSeverity {
    switch (config) {
      case "Error":
        return vscode.DiagnosticSeverity.Error;
      case "Warning":
        return vscode.DiagnosticSeverity.Warning;
      case "Information":
        return vscode.DiagnosticSeverity.Information;
      case "Hint":
        return vscode.DiagnosticSeverity.Hint;
      default:
        return vscode.DiagnosticSeverity.Warning;
    }
  }

  private createMessage(symbol: UnusedSymbol): string {
    const kindLabel = symbol.kind.toLowerCase();
    return `Unused ${kindLabel}: ${symbol.fullyQualifiedName}`;
  }
}
