# NUKE Build System Migration Summary

## Overview
This document summarizes the migration of the NUKE build system from SuCoS to SumTree, converting from an executable-focused build to a library-focused build system with GitHub integration.

## Changes Made

### 1. Project Rename (SuCoS → SumTree)
- Updated all references from `SuCoS` to `SumTree` across build files
- Fixed project references in:
  - `Build.Publish.cs`
  - `Build.Test.cs`
  - `Build.Version.cs`

### 2. Platform Migration (GitLab → GitHub)
- **Renamed**: `Build.GitLab.cs` → `Build.GitHub.cs`
- **Updated URLs**: Changed from GitLab API to GitHub API endpoints
- **Authentication**: Modified to use GitHub tokens instead of GitLab private tokens
- **Repository**: Updated to `https://github.com/brmassa/SumTree`
- **Changelog**: Updated compare links to use GitHub format

### 3. Container Support Removal
- **Deleted**: `Build.Container.cs` - Container generation removed since SumTree is a library
- **Deleted**: `Build.Publish.Debian.cs` - Debian package support removed
- **Simplified**: Removed multi-runtime support complexity

### 4. Library-Focused Publishing
- **Transformed**: `Build.Publish.cs` from executable publishing to NuGet package creation
- **Added**: `Pack` target for creating NuGet packages with symbols and source
- **Added**: `Publish` target for publishing to nuget.org
- **Updated**: Default build target changed from `Compile` to `Pack`

### 5. GitHub Actions Workflows

#### 5.1 Main CI/CD Pipeline (`dotnet.yml`)
- **Triggers**: Push and PR to main branch
- **Features**:
  - Test on .NET Core 3.1 and .NET 8.0
  - Build and create NuGet packages
  - Publish to nuget.org on main branch push
  - Test result reporting

#### 5.2 Weekly Release (`weekly-release.yml`)
- **Schedule**: Every Thursday at 10 AM EST (15:00 UTC)
- **Features**:
  - Automatic commit detection since last release
  - GitVersion-based versioning
  - Changelog generation
  - GitHub release creation
  - Automatic NuGet publishing

#### 5.3 Manual Release (`manual-nuget-publish.yml`)
- **Trigger**: Manual workflow dispatch
- **Features**:
  - Force publish option
  - Skip tests option
  - Version existence checking
  - Manual GitHub release creation

### 6. Version Management
- **GitVersion Integration**: Added GitVersion.Tool package and configuration
- **Fallback Handling**: Added null-safe version handling when GitVersion fails
- **Configuration**: Created `GitVersion.yml` with simplified branch configuration

### 7. Build System Improvements
- **Dependencies**: Added `Nuke.Common.Tools.GitVersion` package
- **Error Handling**: Improved null reference handling in version management
- **Clean Up**: Updated clean targets to remove packages directory instead of publish directory

### 8. Documentation
- **Created**: `CHANGELOG.md` with initial release notes
- **Updated**: Repository links and references throughout the system

## File Structure Changes

### Added Files:
- `GitVersion.yml` - GitVersion configuration
- `CHANGELOG.md` - Project changelog
- `.github/workflows/weekly-release.yml` - Scheduled release workflow
- `.github/workflows/manual-nuget-publish.yml` - Manual publish workflow
- `.nuke/MIGRATION_SUMMARY.md` - This summary document

### Modified Files:
- `.github/workflows/dotnet.yml` - Comprehensive CI/CD pipeline
- `.nuke/Build.cs` - Changed default target to Pack
- `.nuke/Build.Publish.cs` - Converted to library packaging
- `.nuke/Build.GitHub.cs` - Renamed and updated from GitLab
- `.nuke/Build.Version.cs` - Added null-safe version handling
- `.nuke/Build.Test.cs` - Fixed project references
- `.nuke/Build.Compile.cs` - Updated clean targets
- `.nuke/Build.Changelog.cs` - Updated for GitHub URLs
- `.nuke/_build.csproj` - Added GitVersion package reference

### Deleted Files:
- `.nuke/Build.GitLab.cs` - Replaced with Build.GitHub.cs
- `.nuke/Build.Container.cs` - Container support removed
- `.nuke/Build.Publish.Debian.cs` - Debian package support removed

## Key Benefits

1. **Simplified Architecture**: Removed container and multi-runtime complexity
2. **Automated Releases**: Weekly automated releases with proper versioning
3. **GitHub Integration**: Full GitHub Actions CI/CD pipeline
4. **Library Focus**: Optimized for NuGet package distribution
5. **Flexible Publishing**: Both automatic and manual publishing options
6. **Comprehensive Testing**: Multi-framework testing with result reporting

## Usage

### Build and Pack Locally:
```bash
nuke Pack --configuration Release
```

### Publish to NuGet:
```bash
nuke Publish --configuration Release
```

### Manual Release:
Use the "Manual NuGet Publish" workflow in GitHub Actions

### Weekly Releases:
Automatic every Thursday at 10 AM EST if new commits are detected

## Configuration Requirements

### GitHub Secrets:
- `NUGET_API_KEY` - NuGet.org API key for package publishing
- `GITHUB_TOKEN` - Automatically provided by GitHub Actions

### Environment:
- .NET Core 3.1 and .NET 8.0 SDKs
- GitVersion.Tool for automatic versioning
- NUKE Global Tool for build execution