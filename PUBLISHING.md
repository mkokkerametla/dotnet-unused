# Publishing Guide

This guide covers how to publish DotnetUnused as both a .NET Global Tool and standalone binaries.

## Prerequisites

1. **NuGet.org Account**: Create account at https://www.nuget.org
2. **NuGet API Key**: Generate at https://www.nuget.org/account/apikeys
3. **GitHub Token**: Already configured in your repo

## Release Process

### Option 1: Automated Release (Recommended)

This uses GitHub Actions to automatically build and publish when you create a tag.

1. **Update version** in `DotnetUnused/DotnetUnused.csproj`:

   ```xml
   <Version>1.0.1</Version>
   ```

2. **Update CHANGELOG.md** with release notes

3. **Commit changes**:

   ```bash
   git add .
   git commit -m "Bump version to 1.0.1"
   git push
   ```

4. **Create and push tag**:

   ```bash
   git tag v1.0.1
   git push upstream v1.0.1
   ```

5. **Configure NuGet API Key** (one-time setup):
   - Go to GitHub repo → Settings → Secrets and variables → Actions
   - Add secret: `NUGET_API_KEY` with your NuGet API key

6. **GitHub Actions will automatically**:
   - Build the project
   - Pack as NuGet package
   - Publish to NuGet.org
   - Create standalone binaries for all platforms
   - Create GitHub Release with assets

### Option 2: Manual Release

#### Step 1: Pack as .NET Tool

```bash
# Windows
.\scripts\pack.ps1 1.0.1

# Linux/Mac
./scripts/pack.sh 1.0.1
```

This creates: `artifacts/DotnetUnused.1.0.1.nupkg`

#### Step 2: Test Locally

```bash
# Install from local source
dotnet tool install --global --add-source ./artifacts DotnetUnused

# Test it
dotnet-unused --help

# Uninstall
dotnet tool uninstall --global DotnetUnused
```

#### Step 3: Publish to NuGet.org

```bash
dotnet nuget push ./artifacts/DotnetUnused.1.0.1.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_API_KEY
```

#### Step 4: Create Standalone Binaries

```bash
# Windows
.\scripts\publish-standalone.ps1 1.0.1

# This creates archives in ./releases/
```

#### Step 5: Create GitHub Release

```bash
# Using GitHub CLI
gh release create v1.0.1 \
  ./releases/*.zip \
  ./releases/*.tar.gz \
  --title "Release 1.0.1" \
  --notes "See CHANGELOG.md"
```

## Versioning Strategy

Follow [Semantic Versioning](https://semver.org/):

- **MAJOR** (1.x.x): Breaking changes
- **MINOR** (x.1.x): New features, backwards compatible
- **PATCH** (x.x.1): Bug fixes

### Examples:

- `1.0.0`: Initial release
- `1.0.1`: Bug fix
- `1.1.0`: Add new feature (e.g., unused using directives)
- `2.0.0`: Breaking change (e.g., change CLI arguments)

## After Publishing

### Verify NuGet Package

1. Wait 5-10 minutes for NuGet indexing
2. Check: https://www.nuget.org/packages/DotnetUnused
3. Test installation:
   ```bash
   dotnet tool install --global DotnetUnused
   dotnet-unused --help
   ```

### Verify GitHub Release

1. Check: https://github.com/kokkerametla/dotnet-unused/releases
2. Verify all binary assets are attached
3. Test download and run

## Troubleshooting

### "Package already exists" error

- You can't republish the same version to NuGet
- Increment the version number and try again

### "NuGet API key invalid"

- Regenerate your API key at https://www.nuget.org/account/apikeys
- Update GitHub secret if using Actions

### Build warnings

- The 3 warnings (nullability, obsolete API) are non-critical
- They don't affect functionality

## Quick Reference Commands

```bash
# Pack locally
dotnet pack DotnetUnused/DotnetUnused.csproj -c Release -o ./artifacts

# Install locally for testing
dotnet tool install --global --add-source ./artifacts DotnetUnused

# Publish to NuGet
dotnet nuget push ./artifacts/DotnetUnused.*.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_KEY

# Create GitHub release
git tag v1.0.0
git push upstream v1.0.0
```

## Users Will Install Via

```bash
# Global tool (after NuGet publish)
dotnet tool install --global DotnetUnused

# Update to latest
dotnet tool update --global DotnetUnused

# Uninstall
dotnet tool uninstall --global DotnetUnused
```

## Next Steps

1. Set up `NUGET_API_KEY` in GitHub secrets
2. Create your first release with `git tag v1.0.0`
3. Let GitHub Actions handle the rest!
