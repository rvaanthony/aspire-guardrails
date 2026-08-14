#Requires -Version 7.0
<#
.SYNOPSIS
    Single verification gate: restore, whitespace check, build, test, vulnerable-package scan.
    This is exactly what CI runs — run it locally before pushing.
.PARAMETER NoTest
    Skip the test step (restore, whitespace check, and build only).
#>
[CmdletBinding()]
param(
    [switch]$NoTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $solution = Get-ChildItem -Path $repoRoot -Filter '*.slnx' -File | Select-Object -First 1
    if (-not $solution) {
        throw "No *.slnx file found in $repoRoot"
    }
    $solutionPath = $solution.FullName
    Write-Host "==> Solution: $($solution.Name)" -ForegroundColor Cyan

    Write-Host "==> Restore" -ForegroundColor Cyan
    dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

    # `whitespace`, not a bare `dotnet format`, and the reason is not performance.
    #
    # A bare run adds the `style` and `analyzers` passes. Those are almost entirely
    # redundant here: Directory.Build.props sets EnforceCodeStyleInBuild and
    # TreatWarningsAsErrors, so every IDE and CA diagnostic already fails the build
    # a few lines below. The single thing the `style` pass adds is IDE0055, which is
    # using-directive ORDER plus whitespace — and the `whitespace` pass covers the
    # whitespace half on its own, including line endings.
    #
    # Using-directive order is deliberately not enforced, because it cannot be. In a
    # solution whose projects are named after the app, the correct alphabetical
    # position of `using <App>.Common;` relative to `using Microsoft...;` depends on
    # the app's name: it sorts after Microsoft for "Ravenna" and before it for
    # "Acme". No single ordering in the source is correct for every name, so
    # enforcing it would fail the format check on a freshly created solution for
    # roughly half of all names, before a line of code was written.
    #
    # Verified that this still catches what matters: a stray double space reports
    # WHITESPACE and a CRLF line ending reports ENDOFLINE.
    Write-Host "==> Format check (whitespace)" -ForegroundColor Cyan
    dotnet format $solutionPath whitespace --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet format found whitespace problems — run 'dotnet format $($solution.Name) whitespace' locally and commit the result" }

    Write-Host "==> Build (Release)" -ForegroundColor Cyan
    dotnet build $solutionPath --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

    if (-not $NoTest) {
        # Clear previous reports first. The coverage gate MERGES every
        # coverage.cobertura.xml it finds — each test assembly writes its own,
        # covering only what it happened to load — so a report left behind by an
        # earlier run would be merged in as though it were part of this one and
        # would inflate the result. Missing the gate because of a stale file is
        # exactly the failure the gate exists to prevent.
        Write-Host "==> Clear previous test results" -ForegroundColor Cyan
        Get-ChildItem -Path $repoRoot -Recurse -Directory -Filter 'TestResults' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

        Write-Host "==> Test" -ForegroundColor Cyan
        dotnet test $solutionPath --configuration Release --no-build --logger trx --collect:"XPlat Code Coverage"
        if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE" }

        Write-Host "==> Coverage floors" -ForegroundColor Cyan
        & (Join-Path $PSScriptRoot 'coverage-floor.ps1') -RepositoryRoot $repoRoot
        if ($LASTEXITCODE -ne 0) { throw "coverage-floor.ps1 failed with exit code $LASTEXITCODE" }
    }
    else {
        Write-Host "==> Test (skipped: -NoTest)" -ForegroundColor Yellow
        Write-Host "==> Coverage floors (skipped: -NoTest)" -ForegroundColor Yellow
    }

    Write-Host "==> Vulnerable package scan" -ForegroundColor Cyan
    # dotnet list package --vulnerable exits 0 even when it finds vulnerabilities
    # (the command has no dedicated failure exit code), so key off the text it
    # prints instead of $LASTEXITCODE.
    $vulnOutput = dotnet list $solutionPath package --vulnerable --include-transitive 2>&1 | Out-String
    Write-Host $vulnOutput
    if ($vulnOutput -match 'has the following vulnerable packages') {
        throw "Vulnerable NuGet packages detected — see output above. Update the flagged package directly if it's yours, or add an explicit transitive PackageVersion pin in Directory.Packages.props if it's transitive."
    }

    Write-Host "==> verify.ps1 passed" -ForegroundColor Green
}
finally {
    Pop-Location
}
