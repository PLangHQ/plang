# Run tests with coverage and generate HTML report
# Usage: .\coverage.ps1 [filter]
# Example: .\coverage.ps1                          # all tests, cover PLang assembly
# Example: .\coverage.ps1 "Runtime2"               # filter tests by name

param(
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$coverageDir = "$PSScriptRoot\coverage-report"
$coverageFile = "$PSScriptRoot\coverage.cobertura.xml"

# Clean previous
if (Test-Path $coverageDir) { Remove-Item $coverageDir -Recurse -Force }
if (Test-Path $coverageFile) { Remove-Item $coverageFile -Force }

Write-Host "Running tests with coverage..." -ForegroundColor Cyan

$filterArg = ""
if ($Filter) {
    $filterArg = "-- --treenode-filter `"/*/*/$Filter/*`""
}

# Collect coverage using dotnet-coverage
dotnet-coverage collect `
    -f cobertura `
    -o $coverageFile `
    "dotnet run --project PLang.Tests $filterArg"

if (-not (Test-Path $coverageFile)) {
    Write-Host "Coverage file not generated. Tests may have failed." -ForegroundColor Red
    exit 1
}

Write-Host "`nGenerating HTML report..." -ForegroundColor Cyan

# Generate HTML report — only Runtime2 source files
reportgenerator `
    -reports:$coverageFile `
    -targetdir:$coverageDir `
    -reporttypes:"Html;TextSummary" `
    -assemblyfilters:"-PLang.Tests" `
    -filefilters:"+*\Runtime2\*"

# Show summary (first 20 lines only)
$summaryFile = "$coverageDir\Summary.txt"
if (Test-Path $summaryFile) {
    Write-Host "`n--- Coverage Summary ---" -ForegroundColor Green
    Get-Content $summaryFile | Select-Object -First 20
}

Write-Host "`nFull HTML report: $coverageDir\index.html" -ForegroundColor Green

# Open in browser
Start-Process "$coverageDir\index.html"
