# Test Coverage Script

The `scripts/test-coverage-basic.ps1` script provides automated test coverage report generation for the Shift project. It uses the industry-standard ReportGenerator tool to create detailed HTML coverage reports that help identify untested code and measure testing effectiveness.

## Overview

The script automates the entire coverage reporting workflow:
1. **Installs ReportGenerator** (if not already installed)
2. **Builds the solution** cleanly
3. **Runs tests with coverage collection** (if needed)
4. **Generates HTML coverage reports** with filtering
5. **Provides summary and navigation** to results

## Features

### Automated Tool Management
- **Auto-installs ReportGenerator**: Installs `dotnet-reportgenerator-globaltool` globally if not present
- **Version management**: Uses the latest available version from NuGet

### Smart Test Execution
- **Conditional test runs**: Only runs tests if no recent coverage data exists
- **Force re-run option**: `-Force` parameter to force fresh test execution
- **Targeted execution**: Specifically targets `src/test/Shift.Tests` project
- **Error handling**: Exits with proper error codes if tests fail

### Intelligent Filtering
- **Assembly filtering**: Excludes test projects and framework assemblies from coverage
- **Class filtering**: Removes generated code and Microsoft extensions from reports
- **Focused metrics**: Shows only relevant Shift project code coverage

### User-Friendly Output
- **Color-coded console output**: Clear visual feedback during execution
- **Progress indicators**: Shows current step and status
- **Summary information**: Displays report location and usage instructions
- **Auto-open option**: `-Open` parameter to automatically open the report

## Usage

### Basic Usage
```powershell
# Generate coverage report (uses existing test results if available)
.\scripts\test-coverage-basic.ps1

# Generate coverage report and open in browser
.\scripts\test-coverage-basic.ps1 -Open

# Force fresh test execution and generate report
.\scripts\test-coverage-basic.ps1 -Force

# Force fresh execution and auto-open report
.\scripts\test-coverage-basic.ps1 -Force -Open
```

### Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `-Open` | Switch | Automatically open the generated HTML report in the default browser | `$false` |
| `-Force` | Switch | Force fresh test execution even if recent coverage data exists | `$false` |

### Prerequisites

- **PowerShell 5.1+** or **PowerShell Core 6+**
- **.NET 9.0 SDK** (for building and testing)
- **Docker Desktop** (for integration tests)
- **Internet connection** (for ReportGenerator installation)

## Output

### Generated Files
```
coverage-reports/
├── index.html              # Main coverage report (open this file)
├── Summary.html            # Coverage summary
├── index.htm               # Alternative format
└── [additional report files]

TestResults/
├── [timestamp]/
│   ├── coverage.cobertura.xml  # Raw coverage data
│   └── [test result files]
```

### Report Contents
- **Line Coverage**: Percentage of code lines executed during tests
- **Branch Coverage**: Percentage of conditional branches tested
- **Method Coverage**: Percentage of methods called during tests
- **Class Coverage**: Percentage of classes instantiated during tests
- **Assembly Coverage**: Coverage breakdown by project assembly

### Coverage Metrics
The script focuses on the core Shift project assemblies:
- ✅ **Shift.dll** - Main library code
- ❌ **Shift.Tests.dll** - Test code (excluded)
- ❌ **Shift.Test.Framework.dll** - Test framework (excluded)
- ❌ **Testcontainers.*** - External dependencies (excluded)
- ❌ **Microsoft.*** - Framework dependencies (excluded)

## Integration with CI/CD

### Azure DevOps Pipeline
```yaml
- task: PowerShell@2
  displayName: 'Generate Coverage Report'
  inputs:
    filePath: 'scripts/test-coverage-basic.ps1'
    arguments: '-Force'
    
- task: PublishCodeCoverageResults@1
  inputs:
    codeCoverageTool: 'Cobertura'
    summaryFileLocation: 'TestResults/**/coverage.cobertura.xml'
    reportDirectory: 'coverage-reports'
```

### GitHub Actions
```yaml
- name: Generate Coverage Report
  run: .\scripts\test-coverage-basic.ps1 -Force
  shell: pwsh
```

## Best Practices

### Development Workflow
1. **Run unit tests frequently**: `dotnet test --filter "Category!=Integration"`
2. **Generate coverage reports before commits**: `.\scripts\test-coverage-basic.ps1`
3. **Aim for >80% line coverage** on core business logic
4. **Review uncovered code** to identify missing test scenarios

### Coverage Goals
- **Core logic**: >90% line coverage (Parser, MigrationPlanner, ModelExporter)
- **Integration points**: >80% line coverage (SqlServerLoader, SqlMigrationPlanRunner)
- **Utility classes**: >70% line coverage (helpers and utilities)
- **Overall project**: >80% line coverage

### Report Analysis
1. **Open `coverage-reports/index.html`** in your browser
2. **Navigate by assembly** to see per-project coverage
3. **Drill down to classes** to identify uncovered code
4. **Identify missing test scenarios** for uncovered code paths
5. **Focus on business logic** rather than framework integration code
