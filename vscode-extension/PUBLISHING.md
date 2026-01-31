# Publishing the VS Code Extension

This document explains how the VS Code extension is published automatically via GitHub Actions.

## Automatic Publishing

The VS Code extension is automatically built and published when you:

1. Push a git tag (e.g., `v1.0.1`)
2. Manually trigger the release workflow

## Setup Requirements

### 1. VS Code Marketplace PAT

You need to add your Visual Studio Marketplace Personal Access Token as a GitHub secret:

1. Go to your GitHub repository settings
2. Navigate to: **Settings → Secrets and variables → Actions**
3. Click **New repository secret**
4. Name: `VSCE_PAT`
5. Value: Your Personal Access Token from Azure DevOps (the one you created for vsce)
6. Click **Add secret**

### 2. How the Workflow Works

When triggered, the workflow:

1. ✅ Installs GitVersion and calculates version
2. ✅ Sets up Node.js environment
3. ✅ Installs npm dependencies
4. ✅ Updates package.json version to match GitVersion
5. ✅ Compiles TypeScript to JavaScript
6. ✅ Packages extension as .vsix file
7. ✅ Uploads .vsix to GitHub Release
8. ✅ Publishes to VS Code Marketplace (only on tag push, not manual dispatch)

### 3. Version Synchronization

The extension version is automatically synchronized with the CLI tool version using GitVersion:

- Both CLI and extension use the same version number
- No manual version updates needed
- GitVersion calculates version from git history

## Manual Publishing

If you need to publish manually:

```bash
cd vscode-extension

# Update version (GitVersion will handle this in CI)
npm version 1.0.1 --no-git-tag-version

# Compile and package
npm run compile
npx @vscode/vsce package

# Publish
npx @vscode/vsce publish
```

## Workflow Behavior

### On Tag Push (`git push --tags`)

- ✅ Builds and packages extension
- ✅ Publishes to VS Code Marketplace
- ✅ Uploads .vsix to GitHub Release

### On Manual Workflow Dispatch

- ✅ Builds and packages extension
- ✅ Uploads .vsix to GitHub Release
- ⏭️ Skips marketplace publishing (for testing)

## Troubleshooting

### "VSCE_PAT secret not found"

Add the secret in GitHub repository settings (see Setup Requirements above)

### "Publishing failed"

Check that:

- Your PAT is still valid (they expire!)
- Publisher ID matches in package.json
- Extension version doesn't already exist on marketplace

### "Version already exists"

GitVersion will automatically increment the version on the next commit/tag

## Security Notes

- ✅ PAT is stored as GitHub encrypted secret
- ✅ PAT is never exposed in logs
- ✅ Publishing can be disabled by removing the secret
