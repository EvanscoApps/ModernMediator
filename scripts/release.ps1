#requires -Version 7.0
<#
.SYNOPSIS
    Release gate for ModernMediator. Packs the solution, consumes each packed nupkg
    in a separate smoke-test project via PackageReference, and only allows
    publishing when every smoke project passes.

.DESCRIPTION
    By default the script packs and runs all smoke tests, exiting 0 on green and 1
    on red. Pass -Push to publish the six gated nupkgs to nuget.org after every
    smoke test passes. NUGET_API_KEY must be set when -Push is supplied.

    The source generator (ModernMediator.Generators) and template
    (ModernMediator.Starter) packages have different gate shapes (compile-fixture
    and template-instantiation respectively); they are packed but not pushed by
    this script. They get their own gate work in a later task.

.PARAMETER Push
    Publish the six gated nupkgs to nuget.org after smoke tests pass.
#>
param(
    [switch]$Push
)

$ErrorActionPreference = 'Stop'

# cd to repo root (this script lives in <repo>/scripts/release.ps1)
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$solution    = Join-Path $repoRoot 'ModernMediator.sln'
$coreCsproj  = Join-Path $repoRoot 'src/ModernMediator/ModernMediator.csproj'
$packagesDir = Join-Path $repoRoot 'packages'

# Gated packages: each has a packed nupkg AND a smoke-test project that consumes it
# via PackageReference. Order matters for push: dependencies first.
$gatedPackages = @(
    @{ Id = 'ModernMediator';                              Smoke = 'tests/ModernMediator.SmokeTests/ModernMediator.SmokeTests.csproj' }
    @{ Id = 'ModernMediator.FluentValidation';             Smoke = 'tests/ModernMediator.FluentValidation.SmokeTests/ModernMediator.FluentValidation.SmokeTests.csproj' }
    @{ Id = 'ModernMediator.AspNetCore';                   Smoke = 'tests/ModernMediator.AspNetCore.SmokeTests/ModernMediator.AspNetCore.SmokeTests.csproj' }
    @{ Id = 'ModernMediator.Audit.Serilog';                Smoke = 'tests/ModernMediator.Audit.Serilog.SmokeTests/ModernMediator.Audit.Serilog.SmokeTests.csproj' }
    @{ Id = 'ModernMediator.Audit.EntityFramework';        Smoke = 'tests/ModernMediator.Audit.EntityFramework.SmokeTests/ModernMediator.Audit.EntityFramework.SmokeTests.csproj' }
    @{ Id = 'ModernMediator.Idempotency.EntityFramework';  Smoke = 'tests/ModernMediator.Idempotency.EntityFramework.SmokeTests/ModernMediator.Idempotency.EntityFramework.SmokeTests.csproj' }
)

# Packages that pack from this repo but are NOT gated by this task. They are
# packed (so the dir is complete) and listed in the report, but never pushed.
$ungatedPackageIds = @(
    'ModernMediator.Generators'
    'ModernMediator.AspNetCore.Generators'
)

if (-not (Test-Path $solution))   { Write-Host "FATAL: solution not found at $solution" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $coreCsproj)) { Write-Host "FATAL: core csproj not found at $coreCsproj" -ForegroundColor Red; exit 1 }
foreach ($pkg in $gatedPackages) {
    $smokeFull = Join-Path $repoRoot $pkg.Smoke
    if (-not (Test-Path $smokeFull)) {
        Write-Host "FATAL: smoke csproj not found at $smokeFull" -ForegroundColor Red
        exit 1
    }
}

# 1. Resolve <Version> from the core csproj as XML.
[xml]$xml = Get-Content $coreCsproj
$version = ($xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($version)) {
    Write-Host "FATAL: <Version> property is missing or empty in $coreCsproj" -ForegroundColor Red
    exit 1
}
$version = $version.Trim()
Write-Host "Version: $version"

# 2. Wipe and recreate ./packages/.
if (Test-Path $packagesDir) {
    Remove-Item -Recurse -Force $packagesDir
}
New-Item -ItemType Directory -Path $packagesDir | Out-Null

# 3. Pack each packable src/*.csproj into ./packages.
# Note: solution-wide `dotnet pack` is not used because it triggers Restore on the
# smoke-test projects, whose MMSmokeVersion guards fire before any nupkg has been
# produced. Iterating over src/ achieves the same outcome (all packable artifacts
# land in ./packages so satellite-package smoke tests can reuse the dir).
$srcProjects = Get-ChildItem -Path (Join-Path $repoRoot 'src') -Filter '*.csproj' -Recurse -File
if ($srcProjects.Count -eq 0) {
    Write-Host "FATAL: no .csproj files found under ./src" -ForegroundColor Red
    exit 1
}
foreach ($p in $srcProjects) {
    & dotnet pack $p.FullName -c Release -o $packagesDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FATAL: dotnet pack failed for $($p.FullName) (exit $LASTEXITCODE)." -ForegroundColor Red
        exit 1
    }
}

# 4. Assert every gated nupkg exists; report ungated packages that landed.
$missing = @()
foreach ($pkg in $gatedPackages) {
    $nupkg = Join-Path $packagesDir "$($pkg.Id).$version.nupkg"
    if (-not (Test-Path $nupkg)) {
        $missing += $pkg.Id
    } else {
        Write-Host "Packed (gated): $nupkg"
    }
}
if ($missing.Count -gt 0) {
    Write-Host "FATAL: gated nupkg(s) not produced by pack: $($missing -join ', ')." -ForegroundColor Red
    exit 1
}
foreach ($id in $ungatedPackageIds) {
    $nupkg = Join-Path $packagesDir "$id.$version.nupkg"
    if (Test-Path $nupkg) {
        Write-Host "Packed (ungated, will not be pushed): $nupkg"
    }
}

# 5. Cache-clear loop. NuGet may serve a previously cached copy of the same
# version string from the global-packages folder; the gate would then be a lie.
# This includes ungated packages (the source generators) because consumer
# projects pull them via PackageReference and a cached old DLL produces stale
# generator output that does not reflect current source.
$cacheRoot = Join-Path $HOME '.nuget/packages'
$cacheIds = @($gatedPackages | ForEach-Object { $_.Id }) + $ungatedPackageIds
foreach ($id in $cacheIds) {
    $cacheTarget = Join-Path $cacheRoot ($id.ToLowerInvariant() + '/' + $version.ToLowerInvariant())
    if (Test-Path $cacheTarget) {
        Remove-Item -Recurse -Force $cacheTarget
        Write-Host "Cache cleared: $cacheTarget"
    } else {
        Write-Host "Cache clear: no cached copy at $cacheTarget"
    }
}

# 5b. HTTP cache clear, scoped to ModernMediator entries only. NuGet stores raw
# downloaded nupkgs and source list responses in the http-cache. If a previous
# restore pulled ModernMediator.Generators 2.2.0 from nuget.org (the original
# broken build, never republished), that nupkg sits in the http-cache and gets
# replayed into global-packages on the next restore, even after global-packages
# was cleared above. Targeted deletion of nupkg_modernmediator*.dat and
# list_modernmediator*.dat forces the next restore to re-fetch from the now
# correctly mapped local-packed feed.
try {
    $httpCacheRaw = (& dotnet nuget locals http-cache -l) 2>&1 | Out-String
    $httpCache = ($httpCacheRaw -replace '^http-cache:\s*', '').Trim()
    if ([string]::IsNullOrWhiteSpace($httpCache) -or -not (Test-Path $httpCache)) {
        Write-Host "HTTP cache: path not resolved ($httpCache); skipping targeted clear"
    } else {
        $httpEntries = @()
        $httpEntries += Get-ChildItem -Path $httpCache -Recurse -File -Filter 'nupkg_modernmediator*.dat' -ErrorAction SilentlyContinue
        $httpEntries += Get-ChildItem -Path $httpCache -Recurse -File -Filter 'list_modernmediator*.dat' -ErrorAction SilentlyContinue
        if ($httpEntries.Count -eq 0) {
            Write-Host "HTTP cache: no ModernMediator entries cached"
        } else {
            foreach ($entry in $httpEntries) {
                try {
                    Remove-Item -Force -LiteralPath $entry.FullName
                    Write-Host "HTTP cache cleared: $($entry.FullName)"
                } catch {
                    Write-Host "HTTP cache clear failed for $($entry.FullName): $($_.Exception.Message)"
                }
            }
        }
    }
} catch {
    Write-Host "HTTP cache clear failed: $($_.Exception.Message)"
}

# 6. Test loop. Each smoke project runs in isolation against its own packed nupkg.
$failures = @()
foreach ($pkg in $gatedPackages) {
    $smokeFull = Join-Path $repoRoot $pkg.Smoke
    Write-Host ""
    Write-Host "=== Smoke: $($pkg.Id) ===" -ForegroundColor Cyan
    & dotnet test $smokeFull -c Release -p:MMSmokeVersion=$version
    if ($LASTEXITCODE -ne 0) {
        $failures += $pkg.Id
    }
}

if ($failures.Count -gt 0) {
    foreach ($id in $failures) {
        Write-Host "GATE: FAIL. $id failed against the packed nupkg. Push aborted." -ForegroundColor Red
    }
    exit 1
}

Write-Host ""
Write-Host "GATE: PASS. All six smoke projects consume their packed nupkgs correctly." -ForegroundColor Green

# 7. README csharp block compile-sweep. Each fenced ```csharp block in README.md
# is wrapped (if needed) and built against the packed nupkgs. A failed sweep
# blocks push the same way a failed smoke test does: the README is part of the
# contract consumers receive.
Write-Host ""
Write-Host "=== README compile-sweep ===" -ForegroundColor Cyan
$sweep = Join-Path $PSScriptRoot 'readme-compile-sweep.ps1'
if (-not (Test-Path $sweep)) {
    Write-Host "FATAL: $sweep not found." -ForegroundColor Red
    exit 1
}
& pwsh -NoProfile -File $sweep -Version $version
if ($LASTEXITCODE -ne 0) {
    Write-Host "GATE: FAIL. README block compile-sweep failed. Push aborted." -ForegroundColor Red
    exit 1
}
Write-Host "GATE: README sweep passed. Every csharp block in README.md compiles against the packed nupkgs." -ForegroundColor Green

# 8. Push only when -Push and only after green.
if (-not $Push) {
    Write-Host "Skipping push (use -Push to publish)."
    Write-Host "Note: ModernMediator.Generators and ModernMediator.AspNetCore.Generators were packed but are not gated by this script and would not be pushed even with -Push."
    exit 0
}

$apiKey = $env:NUGET_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "FATAL: NUGET_API_KEY environment variable is not set; cannot push." -ForegroundColor Red
    exit 1
}

# Push the six gated nupkgs in dependency order. On any failure, stop and report
# which succeeded and which did not. We do not roll back successful pushes;
# nuget.org does not support that.
$pushed = @()
$failedPush = $null
foreach ($pkg in $gatedPackages) {
    $nupkg = Join-Path $packagesDir "$($pkg.Id).$version.nupkg"
    Write-Host "Pushing: $($pkg.Id) $version"
    & dotnet nuget push $nupkg --api-key $apiKey --source 'https://api.nuget.org/v3/index.json'
    if ($LASTEXITCODE -ne 0) {
        $failedPush = $pkg.Id
        break
    }
    $pushed += $pkg.Id
}

if ($null -ne $failedPush) {
    Write-Host "FATAL: dotnet nuget push failed for $failedPush." -ForegroundColor Red
    if ($pushed.Count -gt 0) {
        Write-Host "Pushed before failure: $($pushed -join ', ')" -ForegroundColor Yellow
    } else {
        Write-Host "No packages were pushed before the failure." -ForegroundColor Yellow
    }
    Write-Host "Not pushed: $failedPush and any packages later in the order." -ForegroundColor Yellow
    exit 1
}

Write-Host "Published: $($pushed -join ', ')" -ForegroundColor Green
Write-Host "Note: ModernMediator.Generators and ModernMediator.AspNetCore.Generators were packed but not pushed by this script (separate gate work pending)."
exit 0
