# Test Coverage Report Generation Script for Shift Project
# Generates HTML coverage reports using ReportGenerator

param(
    [switch]$Open = $false,
    [switch]$Force = $false
)

Write-Host "Starting Coverage Report Generation" -ForegroundColor Cyan

# Install ReportGenerator if needed
function Install-ReportGenerator() {
    $tool = 'dotnet-reportgenerator-globaltool'
    if ($null -eq (dotnet tool list --global | Select-String 'dotnet-reportgenerator-globaltool')) {
        Write-Host "Installing $tool" -ForegroundColor Yellow
        dotnet tool install --global $tool
    } else {
        Write-Host "ReportGenerator already installed" -ForegroundColor Green
    }
}

# Get the latest cobertura file
function Get-CoberturaFile($tempDir) {
    if (Test-Path $tempDir) {
        return Get-ChildItem $tempDir -Recurse -Filter "coverage.cobertura.xml" | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1
    }
    return $null
}

# Install ReportGenerator
Install-ReportGenerator

# Set up directories
$tempDir = "TestResults"
$outputDir = "coverage-reports"

# Clean and build
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet clean --verbosity quiet
dotnet build --verbosity quiet

# Check if we're in the right directory
if (-not (Test-Path "src/Shift.slnx")) {
    Write-Host "Warning: Shift.slnx not found. Make sure you're running from the project root." -ForegroundColor Yellow
}

# Check if we need to run tests
$coberturaFile = Get-CoberturaFile $tempDir

if ($Force -or -not (Test-Path $tempDir) -or -not $coberturaFile) {
    Write-Host "Running tests with coverage..." -ForegroundColor Yellow
    
    # Clean up if needed
    if (Test-Path $tempDir) {
        Remove-Item -Path "$tempDir\*" -Recurse -Force
    }
    
    # Run tests with coverage (targeting the test project specifically)
    dotnet test src/test/Shift.Tests --collect:"XPlat Code Coverage" --results-directory $tempDir --verbosity normal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Tests failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} else {
    Write-Host "Using existing test results..." -ForegroundColor Green
}

# Get the cobertura file
$coberturaFile = Get-CoberturaFile $tempDir

if (-not $coberturaFile) {
    Write-Host "No coverage file found!" -ForegroundColor Red
    exit 1
}

Write-Host "Found coverage file: $($coberturaFile.FullName)" -ForegroundColor Green

# Create output directory
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host "Generating coverage report..." -ForegroundColor Yellow

# Use the correct parameter format from your working script
$output = & reportgenerator `
    -reports:$($coberturaFile.FullName) `
    -targetdir:$outputDir `
    -reporttypes:HtmlInline_AzurePipelines `
    -classfilters:'-System.Text.RegularExpressions.Generated*;-Microsoft.Extensions.*' `
    -assemblyfilters:'-Shift.Tests;-Shift.Test.Framework;-Testcontainers.*;-Microsoft.*;-System.*'

# Check for errors
if ($LASTEXITCODE -gt 0) {
    Write-Host "ReportGenerator failed with exit code $LASTEXITCODE" -ForegroundColor Red
    Write-Host "Output: $output" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Check if report was generated
$reportIndexFile = Join-Path $outputDir "index.html"
if (Test-Path $reportIndexFile) {
    Write-Host "Coverage report generated successfully!" -ForegroundColor Green
    Write-Host "Output: $reportIndexFile" -ForegroundColor Green
    
    if ($Open) {
        Write-Host "Opening coverage report..." -ForegroundColor Yellow
        Start-Process $reportIndexFile
    }
    
    # Show summary
    Write-Host "`n=== Coverage Report Summary ===" -ForegroundColor Cyan
    Write-Host "Report Location: $reportIndexFile" -ForegroundColor White
    Write-Host "Test Results: $tempDir" -ForegroundColor White
    Write-Host "`nTo view the report, open: $reportIndexFile" -ForegroundColor Green
    Write-Host "Or run: .\scripts\test-coverage-basic.ps1 -Open" -ForegroundColor Green
} else {
    Write-Host "Coverage report not found at expected location" -ForegroundColor Red
}
