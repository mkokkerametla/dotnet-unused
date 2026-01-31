export interface UnusedCodeReport {
  summary: {
    totalSymbolsAnalyzed: number;
    totalReferencesFound: number;
    unusedCount: number; // Deprecated: use unusedSymbolsCount
    unusedSymbolsCount?: number;
    unusedUsingsCount?: number;
    durationSeconds: number;
  };
  unusedSymbols: UnusedSymbol[];
  unusedUsings?: UnusedUsing[];
}

export interface UnusedSymbol {
  kind: "Method" | "Property" | "Field";
  fullyQualifiedName: string;
  filePath: string;
  lineNumber: number;
}

export interface UnusedUsing {
  filePath: string;
  lineNumber: number;
  namespace: string;
  message: string;
}

export interface AnalysisOptions {
  scope: "workspace" | "file";
  excludePublic: boolean;
  targetPath: string;
}
