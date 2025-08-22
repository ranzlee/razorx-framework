# Changelog

All notable changes to RazorX.Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- GitHub Packages publication workflow
- Comprehensive GitHub Actions for CI/CD with GitHub Packages integration
- Package validation and testing infrastructure
- Automatic package publishing to GitHub Packages on main branch and release tags

### Changed
- **BREAKING**: Package distribution moved from NuGet.org to GitHub Packages
- Enhanced NuGet package metadata with comprehensive properties
- Improved MSBuild targets with better error handling and cleanup
- Updated installation instructions for GitHub Packages authentication

### Fixed
- Package installation process now includes proper file copying validation

## [1.0.0-alpha] - 2025-01-22

### Added
- Initial alpha release of RazorX.Framework
- Server-Driven UI framework with C#/.NET 9 backend
- TypeScript client library (1,350 lines) with intelligent DOM management
- Comprehensive test suite (138 .NET + 68 JavaScript tests)
- Production-ready architecture with zero memory leaks
- 7 fragment merge strategies for flexible UI updates
- Built-in CSRF protection and security features
- Automatic client file copying to consuming projects

### Technical Details
- **Target Framework**: .NET 9.0 with C# 13
- **TypeScript Version**: 5.8.3 with strict mode
- **Dependencies**: Minimal - only Idiomorph for DOM morphing
- **Performance**: 20-40% DOM optimization, ~50x reflection improvement
- **Memory Management**: WeakMap-based with automatic cleanup
- **Security**: Comprehensive CSRF protection and input validation

### Package Features
- Automatic TypeScript client deployment
- MSBuild targets for seamless integration
- Symbol packages for enhanced debugging
- Source Link support for better development experience

## Installation Instructions

### GitHub Packages Setup

RazorX.Framework is now distributed via GitHub Packages. To install:

1. **Configure GitHub Packages as a NuGet source:**
   ```bash
   dotnet nuget add source https://nuget.pkg.github.com/ranzlee/index.json \
     --name github \
     --username YOUR_GITHUB_USERNAME \
     --password YOUR_GITHUB_TOKEN \
     --store-password-in-clear-text
   ```

2. **Install the package:**
   ```bash
   dotnet add package RazorX.Framework --source github
   ```

3. **Client files automatically copied:**
   - `wwwroot/js/razorx.js` - Main client library
   - `wwwroot/js/razorx.js.map` - Source map for debugging
   - `wwwroot/js/razorx.d.ts` - TypeScript definitions

### GitHub Token Requirements

Create a Personal Access Token (classic) with `read:packages` scope:
1. Go to GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Generate new token with `read:packages` permission
3. Use this token as your password in the NuGet source configuration