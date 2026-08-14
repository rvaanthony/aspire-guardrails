#Requires -Version 7.0
<#
.SYNOPSIS
    Per-assembly line-coverage floor. Fails when any product assembly drops below
    the floor recorded for it below.

.DESCRIPTION
    Invoked by eng/verify.ps1 after the test step, and runnable on its own against
    reports a previous test run left behind.

    THE REPORTS MUST BE MERGED. `dotnet test` runs every test assembly and each writes
    its own cobertura report covering only what that assembly happened to load, so a
    shared layer appears several times at several different rates — Template.Data has
    measured 4%, 21% and 0% simultaneously. A line covered by any report is covered.

    LINE COUNTS ARE RECOMPUTED rather than read off the report, because cobertura
    carries counts only on the root element; per package there is just a rounded
    line-rate. The reconciliation below compares this script's raw count against those
    root totals, so a walk that goes wrong shows up as a mismatch rather than as a
    plausible wrong number.

.PARAMETER Measure
    Print the live values and exit 0 without gating. Use this to re-derive the
    floors after adding tests.

.PARAMETER RepositoryRoot
    Where to search for reports. Defaults to the parent of this script's directory.
#>
[CmdletBinding()]
param(
    [switch]$Measure,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------------
# THE FLOORS. One row per product assembly in the coverage data, and nothing else: an
# assembly here that the run does not cover fails unless marked Optional, and an
# assembly the run covers that is not here fails too. Both tripwires are at the bottom.
#
# `Measured` records the value each floor was set from, so a reviewer sees the headroom
# rather than trusting the floor. It is the MINIMUM across the option combinations this
# starter can be created as, not any single one: the options change which code exists,
# so a floor set from one combination would redden another.
#
# Floors sit BELOW measured on purpose. A floor at the measured value reddens the build
# for any refactor that adds a line before it adds a test, which is the cliff that gets
# a gate suspended and never reinstated. Slack is reported in LINES on every run,
# because a percentage point means something different on a 90-line assembly than on a
# 900-line one. They are not uniform for the same reason: a host's composition root is
# mostly startup wiring, and holding it to a domain layer's standard buys nothing.
#
# TO RE-DERIVE after adding tests: run the tests, then
#   pwsh ./eng/coverage-floor.ps1 -Measure
# and move the numbers up. Never down without a recorded reason — that is the ratchet,
# and it is a review question rather than a private one.
# ---------------------------------------------------------------------------------
$floors = @(
    # Measured is the LOWEST value seen across four option combinations: the
    # default; Entra ID with Sentry; a worker on PostgreSQL; and everything at once
    # on SQL Server. Where those disagree, the combination responsible is named.
    @{ Assembly = 'Template.Common';          Floor = 80; Measured = '100.00% (8/8), every combination';        Optional = $false }
    @{ Assembly = 'Template.Business';        Floor = 80; Measured = '92.31% (12/13), with Entra ID';           Optional = $false }
    # Ratcheted from 20 once both stores got direct tests. It was the weakest number
    # here by a wide margin — the storage-backed store had nothing against it but the
    # initializer — and it now measures 89.57% on the storage combinations and
    # 75.22% on the EF ones. The floor tracks the lower of the two, because the EF
    # combinations exclude the Tables store's tests along with the Tables store.
    @{ Assembly = 'Template.Data';            Floor = 65; Measured = '75.22% (85/113), EF-backed';              Optional = $false }
    @{ Assembly = 'Template.ServiceDefaults'; Floor = 85; Measured = '94.74% (54/57), every combination';       Optional = $false }
    # Signing in with Entra ID costs ~16 points: it adds startup wiring and a login
    # partial, and it excludes the anonymous-request tests. The data backend does
    # not compound it — the all-options combination measures the same 71.30%.
    @{ Assembly = 'Template.Client';          Floor = 65; Measured = '71.30% (246/345), with Entra ID';         Optional = $false }
    @{ Assembly = 'Template.AppHost';         Floor = 80; Measured = '100.00% (12/12), every combination';      Optional = $false }
    # Optional: present only when the app was created with a background worker.
    # Ratcheted from 50 once the registration moved out of Program.cs into
    # AddContactDigest, which made the options binding and its fail-fast validation
    # reachable from a test. What is left uncovered is Program.cs itself — top-level
    # statements no test can call.
    @{ Assembly = 'Template.Worker';          Floor = 70; Measured = '78.95% (30/38), every combination';       Optional = $true }
)

# Reporting only, never a gate: a target that failed the build would be a floor,
# and this one is meant to be aimed at rather than satisfied.
$targetPercent = 80

if (-not $RepositoryRoot) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$candidates = @(Get-ChildItem -Path $RepositoryRoot -Recurse -Filter 'coverage.cobertura.xml' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })

if ($candidates.Count -eq 0) {
    throw "No coverage.cobertura.xml found under $RepositoryRoot. Run the tests with --collect:`"XPlat Code Coverage`" first."
}

# Deduplicated by content: with the trx logger on, each test project yields the same
# report twice — once where the collector wrote it, once copied into the trx attachments
# folder. That changes no number, but it doubles the count reported below, and a count
# that does not match the number of test projects reads like stale reports.
$reports = @($candidates |
    Group-Object -Property { (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash } |
    ForEach-Object { $_.Group[0] })

# Merged state: assembly -> canonical "path|line" -> $true when any report saw a hit.
$merged = @{}
$anomalies = [System.Collections.Generic.List[string]]::new()
# assembly -> the highest distinct-line count any SINGLE report reported for it.
# Every report that includes an assembly instruments the whole assembly, so all of
# them describe the same set of lines. The merged count must therefore equal this
# maximum; see the tripwire below for what it means when it does not.
$perReportValid = @{}

# Cobertura states each path relative to a <sources> root, and coverlet picks that root
# PER REPORT. Measured on one CI run: one report emitted <source>/</source> with a full
# path in `filename`, another emitted the repo root with a relative `filename` — same
# file, same line, two different strings. Keying on the raw filename counted those lines
# twice, doubling the denominator of every shared assembly and halving its coverage.
#
# It passed locally because every report on that machine happened to pick the same root,
# which is a per-report detail rather than a platform one. Resolving against each
# report's own root yields one absolute path, identical across reports.
function Get-CanonicalPath {
    param([string]$FileName, [string[]]$SourceRoots)

    $path = $FileName -replace '\\', '/'
    $isAbsolute = $path.StartsWith('/') -or $path -match '^[A-Za-z]:'

    if (-not $isAbsolute -and $SourceRoots.Count -gt 0) {
        $root = ($SourceRoots[0] -replace '\\', '/').TrimEnd('/')
        $path = "$root/$path"
    }

    # A '/' root collapses to an empty string above, so re-collapse the doubled
    # separator that leaves behind.
    return ($path -replace '/{2,}', '/')
}

foreach ($report in $reports) {
    $document = [System.Xml.XmlDocument]::new()
    $document.Load($report.FullName)

    $rawLines = 0
    $rawCovered = 0
    $sourceRoots = @($document.SelectNodes('/coverage/sources/source') | ForEach-Object { $_.InnerText })

    foreach ($package in $document.SelectNodes('/coverage/packages/package')) {
        $assembly = $package.GetAttribute('name')
        if (-not $merged.ContainsKey($assembly)) {
            $merged[$assembly] = @{}
        }
        $assemblyLines = $merged[$assembly]
        # PowerShell hashtable keys are case-insensitive, which is what we want:
        # two reports differing only in the case of a Windows drive letter describe
        # the same file.
        $thisReportLines = @{}

        foreach ($class in $package.SelectNodes('classes/class')) {
            $fileName = Get-CanonicalPath -FileName $class.GetAttribute('filename') -SourceRoots $sourceRoots
            foreach ($line in $class.SelectNodes('lines/line')) {
                $rawLines++
                $hits = [int]$line.GetAttribute('hits')
                if ($hits -gt 0) { $rawCovered++ }

                # Deduplicated by file and line rather than by class: a partial
                # class or a nested type reports the same source line under more
                # than one <class>, and counting it twice would weight one file
                # by how many types it happens to declare.
                $key = "$fileName|$($line.GetAttribute('number'))"
                $thisReportLines[$key] = $true
                if ($hits -gt 0) {
                    $assemblyLines[$key] = $true
                }
                elseif (-not $assemblyLines.ContainsKey($key)) {
                    $assemblyLines[$key] = $false
                }
            }
        }

        if (-not $perReportValid.ContainsKey($assembly) -or $perReportValid[$assembly] -lt $thisReportLines.Count) {
            $perReportValid[$assembly] = $thisReportLines.Count
        }
    }

    # Reconcile the walk against the only totals cobertura states outright. Raw
    # counts, before the merge, because the root totals are also pre-merge: this
    # asserts the walk visited everything, not that the merge is right.
    $root = $document.SelectSingleNode('/coverage')
    $declaredValid = [int]$root.GetAttribute('lines-valid')
    $declaredCovered = [int]$root.GetAttribute('lines-covered')
    $relative = [IO.Path]::GetRelativePath($RepositoryRoot, $report.FullName)

    if ($declaredValid -ne $rawLines) {
        $anomalies.Add("$relative : root lines-valid says $declaredValid, the walk visited $rawLines line elements.")
    }
    if ($declaredCovered -ne $rawCovered) {
        $anomalies.Add("$relative : root lines-covered says $declaredCovered, the walk counted $rawCovered hit lines.")
    }
}

# The exclusion in coverage.runsettings is a precondition of the floors meaning
# anything — a run that silently stopped applying it measures a different thing
# under the same name. Assert it took effect rather than trusting the file to have
# been picked up.
$leakedGeneratedCode = foreach ($assembly in $merged.Keys) {
    $merged[$assembly].Keys | Where-Object { $_ -match '[\\/]Migrations[\\/]' } | ForEach-Object { "$assembly : $_" }
}
if ($leakedGeneratedCode) {
    $anomalies.Add("coverage.runsettings did not take effect — generated migrations are in the denominator: $($leakedGeneratedCode -join ', ')")
}

# The merge's own check, and the one that matters most, because a bad merge produces a
# plausible number rather than an error. Every report that includes an assembly
# instruments all of it, so merging can only change which lines are COVERED, never how
# many EXIST. A merged denominator larger than any single report's means two reports
# disagreed about a line's identity. The raw-filename bug above did exactly that while
# the per-report reconciliation stayed green — only a cross-report check could see it.
foreach ($assembly in $merged.Keys) {
    $mergedCount = $merged[$assembly].Count
    $singleCount = $perReportValid[$assembly]
    if ($mergedCount -ne $singleCount) {
        $anomalies.Add(
            "$assembly : the merge produced $mergedCount distinct lines but no single report has more than $singleCount. " +
            "Reports disagree about line identity, so the denominator is inflated and every rate below is wrong. " +
            "Check that Get-CanonicalPath resolves each report's <sources> root correctly.")
    }
}

$results = foreach ($row in $floors) {
    $assembly = $row.Assembly
    if (-not $merged.ContainsKey($assembly)) {
        [pscustomobject]@{
            Assembly = $assembly; Valid = 0; Covered = 0; Percent = $null
            Floor = $row.Floor; Optional = $row.Optional; Present = $false
        }
        continue
    }

    $lines = $merged[$assembly]
    $valid = $lines.Count
    $covered = @($lines.Values | Where-Object { $_ }).Count
    [pscustomobject]@{
        Assembly = $assembly
        Valid    = $valid
        Covered  = $covered
        Percent  = if ($valid -gt 0) { [math]::Round(100.0 * $covered / $valid, 2) } else { 0.0 }
        Floor    = $row.Floor
        Optional = $row.Optional
        Present  = $true
    }
}

Write-Host ''
Write-Host ("{0,-26} {1,8} {2,8} {3,8} {4,7} {5,10}" -f 'Assembly', 'Covered', 'Valid', 'Percent', 'Floor', 'ToTarget')
Write-Host ('-' * 72)
foreach ($result in $results) {
    if (-not $result.Present) {
        Write-Host ("{0,-26} {1,8}" -f $result.Assembly, '(absent)')
        continue
    }
    $toTarget = [math]::Max(0, [math]::Ceiling($result.Valid * $targetPercent / 100.0) - $result.Covered)
    Write-Host ("{0,-26} {1,8} {2,8} {3,7:N2}% {4,6}% {5,7} ln" -f `
        $result.Assembly, $result.Covered, $result.Valid, $result.Percent, $result.Floor, $toTarget)
}
Write-Host ''
Write-Host "Merged from $($reports.Count) report(s). Target for every assembly is $targetPercent% (reporting only)."

if ($Measure) {
    Write-Host ''
    Write-Host 'Floor rows to paste back into this script:' -ForegroundColor Cyan
    foreach ($result in $results | Where-Object { $_.Present }) {
        Write-Host ("    Assembly = '{0}'  measured {1:N2}%  ({2}/{3} lines)" -f `
            $result.Assembly, $result.Percent, $result.Covered, $result.Valid)
    }
    if ($anomalies.Count -gt 0) {
        Write-Host ''
        Write-Warning "Reconciliation problems (measurement is not trustworthy):`n  $($anomalies -join "`n  ")"
    }
    exit 0
}

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($anomaly in $anomalies) {
    $failures.Add($anomaly)
}

foreach ($result in $results) {
    if (-not $result.Present) {
        if (-not $result.Optional) {
            # Fails rather than skips, deliberately. An assembly that stops
            # appearing in the coverage data is the loudest possible symptom of a
            # test project that no longer runs, and "no data" must never read as
            # "nothing to check".
            $failures.Add("$($result.Assembly) does not appear in any coverage report. Its tests are not running, or the project was removed without removing its floor from eng/coverage-floor.ps1.")
        }
        continue
    }

    if ($result.Percent -lt $result.Floor) {
        $shortfall = [math]::Ceiling($result.Valid * $result.Floor / 100.0) - $result.Covered
        $failures.Add("$($result.Assembly) is at $($result.Percent)%, below its $($result.Floor)% floor — $shortfall more covered line(s) needed.")
    }
}

$knownAssemblies = $floors.Assembly
foreach ($assembly in $merged.Keys | Sort-Object) {
    if ($assembly -notin $knownAssemblies) {
        # The other half of the same idea: a new product assembly must arrive with
        # a floor, or it is exempt forever by accident.
        $failures.Add("$assembly appears in the coverage data but has no floor in eng/coverage-floor.ps1. Add a row for it.")
    }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }

    # The floors that ship are calibrated against the example contact slice, and
    # deleting that slice is the expected first thing to do with this solution. So
    # the first time this gate fails it is usually measuring the absence of example
    # code rather than a regression, and saying so here is the difference between a
    # useful gate and one that gets switched off on day one.
    #
    # Deliberately NOT a switch that rewrites the floors. A gate with a command that
    # lowers itself is one command away from meaning nothing; moving a floor down
    # should be an edit somebody can see in a diff.
    if ($results | Where-Object { $_.Present -and $_.Percent -lt $_.Floor }) {
        Write-Host ''
        Write-Host 'If you have just removed the example code these floors were measured against,' -ForegroundColor Yellow
        Write-Host 'this is measuring that removal rather than a regression. Re-derive with' -ForegroundColor Yellow
        Write-Host '  pwsh ./eng/coverage-floor.ps1 -Measure' -ForegroundColor Yellow
        Write-Host 'and edit the floor table at the top of this file. Lower a floor deliberately,' -ForegroundColor Yellow
        Write-Host 'in a commit, rather than to make a red build go away.' -ForegroundColor Yellow
    }

    Write-Host ''
    throw "Coverage floor gate failed ($($failures.Count) problem(s)). Run 'pwsh ./eng/coverage-floor.ps1 -Measure' to see the live numbers."
}

Write-Host '==> coverage floors met' -ForegroundColor Green
