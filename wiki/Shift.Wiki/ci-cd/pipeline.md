# CI/CD Pipeline Documentation

## Introduction

This document outlines the current CI/CD (Continuous Integration/Continuous Deployment) pipeline for the Shift project and describes its implementation, capabilities, and current state.

The Shift project is a .NET 9 library that provides database migration and Entity Framework code generation capabilities. Our CI/CD strategy focuses on automated testing, quality assurance, and streamlined package publishing.

## CI/CD Pipeline

### Workflow Overview

We currently have three GitHub Actions workflows that handle different aspects of our CI/CD pipeline:

1. **PR Build and Test** (`pr-build-test.yml`) - Validates code quality on pull requests
2. **Build and Publish** (`build-and-publish.yml`) - Handles production NuGet package publishing via version tags
3. **Pre-release Publish** (`pre-release-publish.yml`) - Handles pre-release NuGet package publishing via release candidate tags

### 1. PR Build and Test Workflow

**Purpose**: Ensures all code changes are validated before merging into the main branch.

**Triggers**:
- Pull requests targeting the main branch
- No direct pushes to main (protected branch)

**Process Flow**:
```
Pull Request Created/Updated
    ↓
Checkout Code
    ↓
Setup .NET 9.0.x
    ↓
Restore Dependencies
    ↓
Build Solution (Release)
    ↓
Run All Tests (with TRX output)
    ↓
Publish Test Results to GitHub UI
    ↓
✅ Pass / ❌ Fail
```

**Key Features**:
- ✅ Automated build validation
- ✅ Comprehensive test execution (all test projects)
- ✅ Visual test results in GitHub PR interface
- ✅ Clear failure reporting with detailed test results
- ✅ Protected main branch integration
- ✅ Professional test reporting using dorny/test-reporter@v2

### 2. Build and Publish Workflow

**Purpose**: Automatically publishes production NuGet packages when version tags are created.

**Triggers**:
- Version tags (e.g., `v1.0.0`, `v2.1.3`)
- Manual workflow dispatch (via GitHub Actions UI)

**Process Flow**:
```
Version Tag Created
    ↓
Extract Version from Tag
    ↓
Restore Dependencies
    ↓
Build Solution (Release)
    ↓
Run Tests (Shift.Tests only)
    ↓
Pack NuGet Package
    ↓
Publish to NuGet.org
```

**Version Format**:
- Version from git tag (e.g., `v1.0.0` → `1.0.0`)

**Key Features**:
- ✅ Automated version extraction from git tags
- ✅ Secure API key management
- ✅ NuGet.org publishing for production releases

### 3. Pre-release Publish Workflow

**Purpose**: Automatically publishes pre-release NuGet packages when release candidate tags are pushed.

**Triggers**:
- Release candidate tags (e.g., `rc-v1.2.3`)

**Process Flow**:
```
Release Candidate Tag Pushed (rc-v*)
    ↓
Extract Version from Tag and Set Pre-release Version
    ↓
Restore Dependencies
    ↓
Build Solution (Release)
    ↓
Run Tests (Shift.Tests only)
    ↓
Pack NuGet Package (Pre-release)
    ↓
Publish to NuGet.org
```

**Version Format**:
- Base version from tag name + `-rc.` + GitHub run number (e.g., `1.2.3-rc.42`)
  - Base version (`n.n.n`): Extracted from tag name by removing `rc-v` prefix (e.g., `rc-v1.2.3` → `1.2.3`)
  - Run number: GitHub Actions run number (`github.run_number`)

**Key Features**:
- ✅ Pre-release publishing on release candidate tag push
- ✅ Pre-release version format: `n.n.n-rc.{RUN_NUMBER}`
- ✅ Automatic version extraction from tag name
- ✅ Run number-based versioning for unique identification
- ✅ Duplicate package protection (`--skip-duplicate` flag prevents failures on re-runs)
- ✅ Secure API key management
- ✅ NuGet.org publishing for pre-release packages

## Version Management Strategy

Our pipeline uses two different version sources:

1. **Pre-release versions** (from release candidate tags):
   - When a release candidate tag (e.g., `rc-v1.2.3`) is pushed, pre-release packages are published
   - Format: `{tag_version}-rc.{RUN_NUMBER}` (e.g., `1.2.3-rc.42`)
   - Example: Pushing tag `rc-v1.2.3` creates package version `1.2.3-rc.42` (where `42` is the GitHub Actions run number for that workflow execution)

2. **Production/stable versions** (from version tags):
   - When a version tag (e.g., `v1.0.0`) is created, production packages are published using the tag version
   - Format: `{tag_version}` (e.g., `1.0.0` from tag `v1.0.0`)

**Version Workflow**:
- Create a release candidate tag (e.g., `rc-v1.2.3`) to publish a pre-release package
- The workflow extracts the version from the tag name and appends the GitHub Actions run number
- When ready for production, create a version tag (e.g., `v1.2.3`) to publish the stable release

**Example Scenario**:
1. Push tag `rc-v1.2.3` → publishes `1.2.3-rc.42` (pre-release, run #42)
2. Push tag `rc-v1.2.3` again → publishes `1.2.3-rc.43` (pre-release, run #43)
3. Create tag `v1.2.3` → publishes `1.2.3` (production)
4. Push tag `rc-v1.2.4` → publishes `1.2.4-rc.44` (pre-release, run #44)

## Industry Best Practices Comparison

### ✅ What We're Doing Well

| Practice | Status | Implementation |
|----------|--------|----------------|
| **Pull Request Validation** | ✅ Implemented | All PRs must pass build and tests |
| **Automated Testing** | ✅ Implemented | Comprehensive test suite execution |
| **Test Result Reporting** | ✅ Implemented | Visual test results in GitHub PR interface |
| **Protected Main Branch** | ✅ Implemented | No direct pushes, PR-only workflow |
| **Automated Publishing** | ✅ Implemented | Tag-based production releases + pre-release on release candidate tags |
| **Version Management** | ✅ Implemented | Semantic versioning with git tags + pre-release versions |
| **Pre-release Publishing** | ✅ Implemented | Automatic pre-release packages on release candidate tags with run number-based versioning |
| **Secure Secrets** | ✅ Implemented | GitHub secrets for API keys |

### Current Gaps

| Practice | Status | Impact |
|----------|--------|--------|
| **Security Scanning** | Not implemented | Medium risk |
| **Code Quality Checks** | Not implemented | Medium risk |
| **Multi-Platform Testing** | Ubuntu only | Low risk |
| **Test Coverage Reporting** | Not implemented | Low risk |
| **Performance Testing** | Not implemented | Low risk |
| **Documentation Generation** | Not implemented | Low risk |
| **Notification System** | Not implemented | Low risk |
| **Artifact Management** | Basic implementation | Low risk |

## Strengths

### 1. Protected Branch Strategy
- **Benefit**: Prevents direct commits to main branch
- **Impact**: Ensures all changes go through PR validation
- **Industry Alignment**: ✅ Best practice

### 2. Comprehensive Test Coverage with Visual Reporting
- **Benefit**: All test projects run on every PR with visual results in GitHub UI
- **Impact**: High confidence in code quality with clear visibility into test outcomes
- **Industry Alignment**: ✅ Best practice

### 3. Automated Publishing
- **Benefit**: Streamlined release process
- **Impact**: Reduces manual errors and deployment time
- **Industry Alignment**: ✅ Best practice

### 4. Version Management
- **Benefit**: Clear versioning with git tags
- **Impact**: Predictable releases and rollback capability
- **Industry Alignment**: ✅ Best practice

## Current Limitations

### Missing Features
- **Security Scanning**: No automated security vulnerability detection
- **Code Quality Checks**: No automated code analysis or linting
- **Multi-Platform Testing**: Currently only tests on Ubuntu
- **Test Coverage Reporting**: No visibility into coverage trends
- **Performance Testing**: No performance benchmarks
- **Documentation Generation**: No automated API documentation

### Impact Assessment
- **Security Risk**: Medium - No automated vulnerability detection
- **Code Quality Risk**: Medium - No automated code analysis
- **Platform Risk**: Low - Single platform testing may miss platform-specific issues
- **Maintenance Risk**: Low - Manual processes require more oversight

> **Note**: For planned improvements and enhancements, see the [CI/CD Development Backlog](../development/backlog-ci-cd.md).

## Conclusion

Our CI/CD pipeline provides a solid foundation with core best practices implemented. The protected branch strategy, comprehensive testing, and automated publishing create a reliable development workflow.

The current pipeline successfully:
- Validates all code changes through PR requirements
- Executes comprehensive test suites on every change
- Provides visual test results in GitHub PR interface
- Automatically publishes production NuGet packages on version tags
- Automatically publishes pre-release NuGet packages when release candidate tags are pushed
- Maintains secure API key management

While there are opportunities for enhancement (as documented in the [CI/CD Development Backlog](../development/backlog-ci-cd.md)), the current pipeline effectively supports our development process and ensures code quality.
