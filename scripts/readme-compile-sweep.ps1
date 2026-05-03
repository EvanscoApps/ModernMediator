#requires -Version 7.0
<#
.SYNOPSIS
    Compile every fenced csharp block in README.md against the packed nupkgs.

.DESCRIPTION
    Re-parses README.md, classifies each fenced csharp block, generates a per-block
    csproj that PackageReferences the gated ModernMediator nupkgs from ./packages/,
    and runs `dotnet build` on each. Compile-only; never executes the resulting
    assemblies.

    Wrapping rules (deterministic, applied in order):
      NON_COMPILABLE_BY_DESIGN: block contains an explicit elision marker
                                (literal (...), bare ..., // ..., // other code,
                                // elsewhere). Skipped.
      MIXED:              has type declarations AND top-level statements. Types
                          go into a synthetic namespace; statements go into a
                          Wrapper.Run() method in the same file.
      TYPE_DECLARATION:   only type declarations. Wrapped in a synthetic namespace
                          with common usings.
      METHOD_BODY:        only top-level statements. Wrapped in Wrapper.Run() with
                          a broad parameter list (services, mediator, publisher,
                          sender, streamer, app, ct).

    Block classification is purely structural. The compile result is the verdict.
    A block that fails to compile because the README references undefined symbols
    or supplies the wrong constructor argument count is a README finding, not a
    sweep bug. Do not paper over README bugs by widening the wrapper.

.PARAMETER Version
    The MMSmokeVersion to consume. Defaults to <Version> from
    src/ModernMediator/ModernMediator.csproj.
#>

param([string]$Version)

$ErrorActionPreference = 'Stop'

$repoRoot    = Split-Path -Parent $PSScriptRoot
$readme      = Join-Path $repoRoot 'README.md'
$packagesDir = Join-Path $repoRoot 'packages'
$workRoot    = Join-Path $repoRoot 'tests/ReadmeBlocks'

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$xml = Get-Content (Join-Path $repoRoot 'src/ModernMediator/ModernMediator.csproj')
    $Version = ($xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($Version)) {
        Write-Host "FATAL: cannot resolve MMSmokeVersion" -ForegroundColor Red; exit 1
    }
    $Version = $Version.Trim()
}
if (-not (Test-Path $packagesDir)) {
    Write-Host "FATAL: packages directory not found at $packagesDir. Run scripts/release.ps1 first to pack." -ForegroundColor Red
    exit 1
}

function Get-CsharpBlocks {
    param([string]$Path)
    $lines = Get-Content -LiteralPath $Path
    $blocks = New-Object System.Collections.Generic.List[object]
    $inBlock = $false; $openLine = 0
    $buf = New-Object System.Collections.Generic.List[string]
    $sharedMarker = '<!--\s*compile-sweep:shared-setup\s*-->'
    $pendingShared = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]; $oneBased = $i + 1
        if (-not $inBlock) {
            if ($line -match $sharedMarker) {
                $pendingShared = $true
                continue
            }
            if ($line -match '^\s*```([A-Za-z0-9_+-]+)\s*$') {
                $tag = $Matches[1].ToLowerInvariant()
                if ($tag -eq 'csharp' -or $tag -eq 'cs') {
                    $inBlock = $true; $openLine = $oneBased; $buf.Clear()
                }
            } elseif ($line.Trim() -ne '') {
                # Any non-blank, non-fence, non-marker line clears the pending marker.
                $pendingShared = $false
            }
        } else {
            if ($line -match '^\s*```\s*$') {
                $blocks.Add([pscustomobject]@{
                    Index = $blocks.Count + 1
                    OpenLine = $openLine
                    Content = ($buf -join "`n")
                    IsShared = $pendingShared
                })
                $inBlock = $false
                $pendingShared = $false
            } else { [void]$buf.Add($line) }
        }
    }
    if ($inBlock) { throw "README.md ends inside an unclosed csharp fence opened at line $openLine" }
    return ,$blocks
}

function Get-Classification {
    param([string]$Content)

    if ($Content -match '\(\s*\.\.\.\s*\)' -or
        $Content -match '(?m)^\s*\.{3}\s*$' -or
        $Content -match '//\s*\.{3,}' -or
        $Content -match '//\s*other code' -or
        $Content -match '//\s*elsewhere') {
        return @{ Class = 'NON_COMPILABLE_BY_DESIGN'; Reason = 'contains explicit elision marker' }
    }

    $typeRegex = '^(\[[^\]]*\]\s*)*((public|internal|private|protected|sealed|abstract|partial|static|readonly|file)\s+)*(record|class|interface|struct|enum)\s+\w'
    $hasType = $false; $hasStmt = $false
    $depth = 0
    # When the previous depth-0 line was a type head without an inline brace
    # (e.g. `public class Foo` followed by `{` on the next line), the brace
    # itself is also at depth 0 but must NOT be treated as a statement.
    $expectTypeBrace = $false
    foreach ($line in ($Content -split "`r?`n")) {
        $trimmed = $line.Trim()
        if ($depth -eq 0 -and $trimmed -ne '' -and -not $trimmed.StartsWith('//')) {
            if ($expectTypeBrace -and ($trimmed -eq '{' -or $trimmed.StartsWith('where '))) {
                # Brace or generic-constraint continuation of a type head; ignore.
            } elseif ($trimmed -match $typeRegex) {
                $hasType = $true
                # If line doesn't end with ; and doesn't contain { yet, the type body
                # opens on a subsequent line.
                if ($line -notmatch '\{' -and -not $line.TrimEnd().EndsWith(';')) {
                    $expectTypeBrace = $true
                } else {
                    $expectTypeBrace = $false
                }
            } elseif ($trimmed -eq '{' -or $trimmed -eq '}') {
                # Lone brace at depth 0 outside a type head context: ignore.
            } elseif (-not ($trimmed.StartsWith('[') -or $trimmed.StartsWith('using ') -or $trimmed.StartsWith('namespace '))) {
                $hasStmt = $true
            }
        }
        $depth += ([regex]::Matches($line, '\{').Count) - ([regex]::Matches($line, '\}').Count)
        if ($depth -gt 0) { $expectTypeBrace = $false }
    }

    if ($hasType -and $hasStmt) { return @{ Class = 'MIXED';            Reason = 'declares types and has top-level statements' } }
    elseif ($hasType)           { return @{ Class = 'TYPE_DECLARATION'; Reason = 'only type declarations' } }
    elseif ($hasStmt)           { return @{ Class = 'METHOD_BODY';      Reason = 'top-level statements only' } }
    else                        { return @{ Class = 'NON_COMPILABLE_BY_DESIGN'; Reason = 'no recognizable code' } }
}

function Split-MixedContent {
    param([string]$Content)
    # Routes lines into either the types or statements bucket using a small state machine:
    #   IDLE                    -- at depth 0, deciding which bucket the next non-trivial line belongs to
    #   IN_TYPE_PENDING_BRACE   -- saw a type head (e.g. `class Foo<T>`) without `{` yet; route subsequent
    #                              `where T : ...` continuation and the eventual `{` to types
    #   IN_TYPE                 -- depth > 0 inside a type body; everything routes to types
    #   IN_STMT                 -- depth > 0 inside a statement block (for/if/foreach body); everything
    #                              routes to statements. Critical: a top-level `await foreach (...) { ... }`
    #                              must NOT be routed to types just because depth > 0.
    $typeRegex = '^((public|internal|private|protected|sealed|abstract|partial|static|readonly|file)\s+)*(record|class|interface|struct|enum)\s+\w'
    $lines = $Content -split "`r?`n"
    $types = New-Object System.Collections.Generic.List[string]
    $stmts = New-Object System.Collections.Generic.List[string]
    $pending = New-Object System.Collections.Generic.List[string]
    $depth = 0
    $mode = 'IDLE'
    foreach ($line in $lines) {
        $bd = ([regex]::Matches($line, '\{').Count) - ([regex]::Matches($line, '\}').Count)
        if ($mode -eq 'IN_TYPE') {
            $types.Add($line); $depth += $bd
            if ($depth -le 0) { $mode = 'IDLE'; $depth = 0 }
            continue
        }
        if ($mode -eq 'IN_STMT') {
            $stmts.Add($line); $depth += $bd
            if ($depth -le 0) { $mode = 'IDLE'; $depth = 0 }
            continue
        }
        if ($mode -eq 'IN_TYPE_PENDING_BRACE') {
            $types.Add($line); $depth += $bd
            if ($depth -gt 0) { $mode = 'IN_TYPE' }
            continue
        }
        # IDLE
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('//') -or $trimmed.StartsWith('[')) {
            $pending.Add($line); continue
        }
        if ($trimmed -match $typeRegex) {
            foreach ($p in $pending) { $types.Add($p) }; $pending.Clear()
            $types.Add($line); $depth += $bd
            if ($depth -gt 0) { $mode = 'IN_TYPE' }
            elseif ($line.TrimEnd().EndsWith(';')) { $mode = 'IDLE' }
            else { $mode = 'IN_TYPE_PENDING_BRACE' }
        } else {
            foreach ($p in $pending) { $stmts.Add($p) }; $pending.Clear()
            $stmts.Add($line); $depth += $bd
            if ($depth -gt 0) { $mode = 'IN_STMT' }
        }
    }
    foreach ($p in $pending) { $stmts.Add($p) }
    return @{
        Types      = ($types -join "`n")
        Statements = ($stmts -join "`n")
    }
}

function New-BlockProject {
    param([int]$Index, [string]$WorkDir, [string]$OutputType)
    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <OutputType>$OutputType</OutputType>
    <IsPackable>false</IsPackable>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <NoWarn>CS1591;CS0618;CS8019;CS0168;CS0219;CS0162;CS8321;CS0028;CS0414;CS0649</NoWarn>
    <RootNamespace>ReadmeBlocks.Block$Index</RootNamespace>
    <AssemblyName>ReadmeBlocks.Block$Index</AssemblyName>
  </PropertyGroup>
  <Target Name="RequireMMSmokeVersion" BeforeTargets="Restore;CollectPackageReferences">
    <Error Condition="'`$(MMSmokeVersion)' == ''" Text="MMSmokeVersion must be set." />
  </Target>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="ModernMediator" Version="`$(MMSmokeVersion)" />
    <PackageReference Include="ModernMediator.Generators" Version="`$(MMSmokeVersion)" PrivateAssets="all" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="ModernMediator.FluentValidation" Version="`$(MMSmokeVersion)" />
    <PackageReference Include="ModernMediator.AspNetCore" Version="`$(MMSmokeVersion)" />
    <PackageReference Include="ModernMediator.AspNetCore.Generators" Version="`$(MMSmokeVersion)" PrivateAssets="all" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="ModernMediator.Audit.Serilog" Version="`$(MMSmokeVersion)" />
    <PackageReference Include="ModernMediator.Audit.EntityFramework" Version="`$(MMSmokeVersion)" />
    <PackageReference Include="ModernMediator.Idempotency.EntityFramework" Version="`$(MMSmokeVersion)" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.10" />
  </ItemGroup>
</Project>
"@
    Set-Content -LiteralPath (Join-Path $WorkDir "Block$Index.csproj") -Value $csproj -Encoding UTF8

    $nuget = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packed" value="../../../packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local-packed">
      <package pattern="ModernMediator*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@
    Set-Content -LiteralPath (Join-Path $WorkDir 'NuGet.config') -Value $nuget -Encoding UTF8
}

$commonUsings = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModernMediator;
using ModernMediator.AspNetCore;
using ModernMediator.AspNetCore.Generated;
using ModernMediator.Dispatchers;
using ModernMediator.Generated;
using ModernMediator.FluentValidation;
using ModernMediator.Audit.Serilog;
using ModernMediator.Audit.EntityFramework;
using ModernMediator.Idempotency.EntityFramework;
"@

# Stub for the conventional `RegisterServicesFromAssemblyContaining<Program>()` reference.
# The README treats `Program` as ambient (it is the host's entry point); per-block compile
# units have no host, so we provide a stub to satisfy the type lookup.
$programStub = "internal partial class Program { }"

function Wrap-MethodBody {
    param([int]$Index, [string]$Content, [string]$Shared = '')
    @"
$commonUsings
namespace ReadmeBlocks.Block$Index;

$programStub

$Shared

public static class Wrapper
{
    public static async Task Run(
        IServiceCollection services,
        IMediator mediator,
        IPublisher publisher,
        ISender sender,
        IStreamer streamer,
        WebApplication app,
        CancellationToken ct = default)
    {
$Content
        await Task.CompletedTask;
    }
}
"@
}

function Wrap-TypeDeclaration {
    param([int]$Index, [string]$Content, [string]$Shared = '')
    @"
$commonUsings
namespace ReadmeBlocks.Block$Index;

$programStub

$Shared

$Content
"@
}

function Wrap-Mixed {
    param([int]$Index, [string]$Types, [string]$Statements, [string]$Shared = '')
    @"
$commonUsings
namespace ReadmeBlocks.Block$Index;

$programStub

$Shared

$Types

public static class Wrapper
{
    public static async Task Run(
        IServiceCollection services,
        IMediator mediator,
        IPublisher publisher,
        ISender sender,
        IStreamer streamer,
        WebApplication app,
        CancellationToken ct = default)
    {
$Statements
        await Task.CompletedTask;
    }
}
"@
}

# --- Main
$blocks = Get-CsharpBlocks -Path $readme
Write-Host "Found $($blocks.Count) csharp blocks in README.md"

# Identify the shared-setup block (if any) by HTML marker. Its content is prepended
# to every other block's generated source so the README's distributed examples can
# share types/helpers without redefining them. The shared block compiles standalone
# (typically classifies as TYPE_DECLARATION).
$sharedBlock   = $blocks | Where-Object { $_.IsShared } | Select-Object -First 1
$sharedContent = ''
if ($sharedBlock) {
    $sharedContent = $sharedBlock.Content
    $lineCount = ($sharedContent -split "`r?`n").Count
    Write-Host ("Prepending shared setup ({0} lines) from block at README line {1}" -f $lineCount, $sharedBlock.OpenLine) -ForegroundColor Cyan
}

New-Item -ItemType Directory -Path $workRoot -Force | Out-Null

$gitignoreLine = 'ReadmeBlocks/'
$testsGitignore = Join-Path $repoRoot 'tests/.gitignore'
if (Test-Path $testsGitignore) {
    $existing = Get-Content $testsGitignore
    if ($existing -notcontains $gitignoreLine) { Add-Content -LiteralPath $testsGitignore -Value $gitignoreLine }
} else {
    Set-Content -LiteralPath $testsGitignore -Value $gitignoreLine -Encoding UTF8
}

$results = @()
foreach ($block in $blocks) {
    $cls = Get-Classification -Content $block.Content
    $r = [pscustomobject]@{
        Index = $block.Index; OpenLine = $block.OpenLine
        Class = $cls.Class;   Reason   = $cls.Reason
        Status = ''; BuildOutput = ''; Content = $block.Content
    }
    if ($cls.Class -eq 'NON_COMPILABLE_BY_DESIGN') {
        $r.Status = 'SKIP'; $results += $r; continue
    }
    $blockDir = Join-Path $workRoot ("Block{0:D2}" -f $block.Index)
    if (Test-Path $blockDir) {
        # Clear stale Block.cs and bin/obj from the previous run; tolerate file locks
        # (Visual Studio's design-time builds frequently hold these paths).
        Get-ChildItem -LiteralPath $blockDir -Recurse -Force -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -Recurse -ErrorAction SilentlyContinue }
    }
    New-Item -ItemType Directory -Path $blockDir -Force | Out-Null
    $outputType = 'Library'
    if ($cls.Class -eq 'STANDALONE_PROGRAM') { $outputType = 'Exe' }
    New-BlockProject -Index $block.Index -WorkDir $blockDir -OutputType $outputType
    # Prepend shared setup to every block except the setup block itself and any
    # STANDALONE_PROGRAM block (which provides its own complete file).
    $shared = ''
    if ($sharedContent -ne '' -and -not $block.IsShared -and $cls.Class -ne 'STANDALONE_PROGRAM') {
        $shared = $sharedContent
    }
    switch ($cls.Class) {
        'METHOD_BODY'        { $cs = Wrap-MethodBody      -Index $block.Index -Content $block.Content -Shared $shared }
        'TYPE_DECLARATION'   { $cs = Wrap-TypeDeclaration -Index $block.Index -Content $block.Content -Shared $shared }
        'MIXED'              {
            $split = Split-MixedContent -Content $block.Content
            $cs = Wrap-Mixed -Index $block.Index -Types $split.Types -Statements $split.Statements -Shared $shared
        }
        'STANDALONE_PROGRAM' { $cs = $block.Content }
    }
    Set-Content -LiteralPath (Join-Path $blockDir 'Block.cs') -Value $cs -Encoding UTF8
    # Stub the AspNetCore.Generated namespace so 'using ModernMediator.AspNetCore.Generated;'
    # resolves in blocks that don't trigger the endpoint generator's emit. When the generator
    # does emit, its file coexists with this stub since C# allows the same namespace to span files.
    $stub = "namespace ModernMediator.AspNetCore.Generated { internal static class _NamespaceStub { } }"
    Set-Content -LiteralPath (Join-Path $blockDir 'GeneratedNamespaceStub.cs') -Value $stub -Encoding UTF8
    $proj = Join-Path $blockDir ("Block{0}.csproj" -f $block.Index)
    Write-Host ("Building Block {0,2} (line {1,4}, {2})..." -f $block.Index, $block.OpenLine, $cls.Class) -ForegroundColor DarkGray
    $build = & dotnet build $proj -c Release -p:MMSmokeVersion=$Version 2>&1
    $exit = $LASTEXITCODE
    if ($exit -eq 0) {
        $r.Status = 'PASS'
    } else {
        $r.Status = 'FAIL'
        $r.BuildOutput = (($build | Select-Object -Last 30) -join "`n")
    }
    $results += $r
}

# --- Summary
Write-Host ""
Write-Host "=== README compile-sweep summary (version $Version) ===" -ForegroundColor Cyan
("{0,-5} {1,-8} {2,-25} {3,-6} {4}" -f 'Idx', 'Line', 'Class', 'Status', 'Reason') | Write-Host
foreach ($r in $results) {
    ("{0,-5} {1,-8} {2,-25} {3,-6} {4}" -f $r.Index, $r.OpenLine, $r.Class, $r.Status, $r.Reason) | Write-Host
}

$failed = $results | Where-Object { $_.Status -eq 'FAIL' }
if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "=== FAIL details ===" -ForegroundColor Red
    foreach ($r in $failed) {
        Write-Host ""
        Write-Host "--- Block $($r.Index) at README line $($r.OpenLine) ($($r.Class)) ---" -ForegroundColor Yellow
        Write-Host "Block content:"
        Write-Host $r.Content
        Write-Host ""
        Write-Host "Build output (tail):"
        Write-Host $r.BuildOutput
    }
}

$passedCount  = ($results | Where-Object { $_.Status -eq 'PASS' }).Count
$failedCount  = ($results | Where-Object { $_.Status -eq 'FAIL' }).Count
$skippedCount = ($results | Where-Object { $_.Status -eq 'SKIP' }).Count
Write-Host ""
Write-Host "Totals: PASS=$passedCount FAIL=$failedCount SKIP=$skippedCount (total=$($results.Count))"

if ($failedCount -gt 0) { exit 1 }
exit 0
