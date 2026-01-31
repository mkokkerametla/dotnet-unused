# GitVersion Configuration

This project uses [GitVersion](https://gitversion.net/) for automatic semantic versioning based on git history.

## How It Works

GitVersion automatically calculates the next version number based on:

- Git tags
- Branch names
- Commit messages (with conventional commits)
- Git history

### Versioning Strategy

**Mode**: ContinuousDeployment

**Branches**:

- `master`/`main`: Release branches (e.g., 1.0.0, 1.0.1)
- `feature/*`: Alpha versions (e.g., 1.0.1-alpha.1)
- `fix/*`: Beta versions (e.g., 1.0.1-beta.1)
- `pr/*`: PR versions (e.g., 1.0.1-pr.1)

### Version Increment

- **Patch**: Default increment (1.0.0 → 1.0.1)
- **Minor**: Add `+semver: minor` in commit message (1.0.0 → 1.1.0)
- **Major**: Add `+semver: major` in commit message (1.0.0 → 2.0.0)

## Testing Locally

### Install GitVersion

```bash
dotnet tool install --global GitVersion.Tool
```

### Check Current Version

```bash
cd /path/to/dotnet-unused
dotnet-gitversion
```

### Sample Output

```json
{
  "Major": 1,
  "Minor": 0,
  "Patch": 1,
  "SemVer": "1.0.1",
  "NuGetVersion": "1.0.1",
  "AssemblySemVer": "1.0.0.0"
}
```

## CI/CD Integration

The GitHub Actions workflow automatically:

1. Installs GitVersion
2. Calculates version from git history
3. Uses version for:
   - NuGet package (`NuGetVersion`)
   - Binary archives (`SemVer`)
   - GitHub releases (`SemVer`)

## Version Bumping

### Create a Patch Release (1.0.0 → 1.0.1)

```bash
git commit -m "fix: some bug fix"
git push
```

### Create a Minor Release (1.0.0 → 1.1.0)

```bash
git commit -m "feat: new feature

+semver: minor"
git push
```

### Create a Major Release (1.0.0 → 2.0.0)

```bash
git commit -m "feat!: breaking change

BREAKING CHANGE: API changes

+semver: major"
git push
```

### Create a Tagged Release

```bash
git tag v1.2.3
git push --tags
```

## Benefits

- ✅ Automatic version calculation
- ✅ Consistent versioning across NuGet, binaries, and releases
- ✅ No manual version updates needed
- ✅ Git history drives versioning
- ✅ Pre-release versions for feature branches
