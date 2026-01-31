import * as vscode from "vscode";
import * as path from "path";
import { UnusedCodeReport, UnusedSymbol } from "./types";

export class UnusedSymbolsProvider implements vscode.TreeDataProvider<TreeItem> {
  private _onDidChangeTreeData: vscode.EventEmitter<
    TreeItem | undefined | null | void
  > = new vscode.EventEmitter<TreeItem | undefined | null | void>();
  readonly onDidChangeTreeData: vscode.Event<
    TreeItem | undefined | null | void
  > = this._onDidChangeTreeData.event;

  private report: UnusedCodeReport | null = null;

  refresh(report: UnusedCodeReport | null): void {
    this.report = report;
    this._onDidChangeTreeData.fire();
  }

  getTreeItem(element: TreeItem): vscode.TreeItem {
    return element;
  }

  getChildren(element?: TreeItem): Thenable<TreeItem[]> {
    if (!this.report) {
      return Promise.resolve([]);
    }

    if (!element) {
      return Promise.resolve(this.getRootItems());
    }

    if (element.contextValue === "category") {
      return Promise.resolve(this.getSymbolsByKind(element.label as string));
    }

    return Promise.resolve([]);
  }

  private getRootItems(): TreeItem[] {
    if (!this.report || this.report.unusedSymbols.length === 0) {
      return [];
    }

    const categories = new Map<string, number>();
    for (const symbol of this.report.unusedSymbols) {
      categories.set(symbol.kind, (categories.get(symbol.kind) || 0) + 1);
    }

    return Array.from(categories.entries()).map(([kind, count]) => {
      const item = new TreeItem(kind, vscode.TreeItemCollapsibleState.Expanded);
      item.description = `${count} unused`;
      item.contextValue = "category";
      return item;
    });
  }

  private getSymbolsByKind(kind: string): TreeItem[] {
    if (!this.report) {
      return [];
    }

    return this.report.unusedSymbols
      .filter((s) => s.kind === kind)
      .map((symbol) => {
        const fileName = path.basename(symbol.filePath);
        const item = new TreeItem(
          symbol.fullyQualifiedName,
          vscode.TreeItemCollapsibleState.None,
        );
        item.description = `${fileName}:${symbol.lineNumber}`;
        item.tooltip = symbol.filePath;
        item.contextValue = "symbol";
        item.command = {
          command: "vscode.open",
          title: "Open File",
          arguments: [
            vscode.Uri.file(symbol.filePath),
            {
              selection: new vscode.Range(
                symbol.lineNumber - 1,
                0,
                symbol.lineNumber - 1,
                1000,
              ),
            },
          ],
        };
        return item;
      });
  }
}

class TreeItem extends vscode.TreeItem {
  constructor(
    public readonly label: string,
    public readonly collapsibleState: vscode.TreeItemCollapsibleState,
  ) {
    super(label, collapsibleState);
  }
}
