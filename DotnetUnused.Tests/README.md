# DotnetUnused.Tests

Unit and integration tests for the DotnetUnused CLI tool.

## Running Tests

```bash
cd DotnetUnused.Tests
dotnet test
```

## Test Structure

### Unit Tests

- `Core/FileFilterTests.cs` - Tests for file filtering logic

### Integration Tests (Future)

Full integration tests requiring Roslyn workspace are marked with `[Fact(Skip = "...")]` due to Microsoft.Build.Locator assembly loading constraints in test environments.

For comprehensive testing, consider:

1. Manual testing with real .NET projects
2. End-to-end CLI testing with sample solutions
3. Isolated unit tests for utility classes (like FileFilter)

## Known Limitations

Microsoft.Build.Locator has strict requirements for assembly loading that conflict with xUnit test runners. Full integration tests with Roslyn workspace require special configuration or alternative testing approaches.

## Adding Tests

When adding new tests:

- Utility classes (FileFilter, etc.) can be tested normally
- Core analysis classes (UnusedDetector, SymbolIndexer) require mocking or E2E testing
- Use Theory/InlineData for parameterized tests
- Keep tests focused and independent
