#requires -Version 7.0
<#
.SYNOPSIS
    Inventory every fenced csharp block in README.md.

.DESCRIPTION
    Scans README.md for fenced blocks opened with ```csharp (case-insensitive),
    numbers them in document order, and emits a tab-delimited inventory:
        index  openLine  lineCount  firstChars
    The same data is written to scripts/readme-blocks-inventory.txt so the
    compile sweep can re-read it without parsing markdown again.
#>

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$readme     = Join-Path $repoRoot 'README.md'
$outputFile = Join-Path $PSScriptRoot 'readme-blocks-inventory.txt'

if (-not (Test-Path $readme)) {
    Write-Host "FATAL: README.md not found at $readme" -ForegroundColor Red
    exit 1
}

$lines = Get-Content -LiteralPath $readme

$blocks = New-Object System.Collections.Generic.List[object]
$inBlock = $false
$blockOpenLine = 0
$blockLines = New-Object System.Collections.Generic.List[string]

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $oneBased = $i + 1
    if (-not $inBlock) {
        if ($line -match '^\s*```([A-Za-z0-9_+-]+)\s*$') {
            $tag = $Matches[1].ToLowerInvariant()
            if ($tag -eq 'csharp' -or $tag -eq 'cs') {
                $inBlock = $true
                $blockOpenLine = $oneBased
                $blockLines.Clear()
            }
        }
    } else {
        if ($line -match '^\s*```\s*$') {
            $content = ($blockLines -join "`n")
            $first = $content -replace '\s+', ' '
            if ($first.Length -gt 60) { $first = $first.Substring(0, 60) }
            $blocks.Add([pscustomobject]@{
                Index      = $blocks.Count + 1
                OpenLine   = $blockOpenLine
                LineCount  = $blockLines.Count
                FirstChars = $first
                Content    = $content
            })
            $inBlock = $false
        } else {
            [void]$blockLines.Add($line)
        }
    }
}

if ($inBlock) {
    Write-Host "FATAL: README.md ends inside an unclosed csharp fence opened at line $blockOpenLine" -ForegroundColor Red
    exit 1
}

$header = "index`topenLine`tlineCount`tfirstChars"
$rows = $blocks | ForEach-Object {
    "{0}`t{1}`t{2}`t{3}" -f $_.Index, $_.OpenLine, $_.LineCount, $_.FirstChars
}

$out = @($header) + $rows
Set-Content -LiteralPath $outputFile -Value $out -Encoding UTF8

Write-Host "README csharp block count: $($blocks.Count)"
Write-Host ""
Write-Host $header
$rows | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Inventory written to: $outputFile"
