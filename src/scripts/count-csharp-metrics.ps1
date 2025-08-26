# Copyright (c) Microsoft Corporation. All rights reserved.
param(
    [string]$Root = "$PSScriptRoot/../src"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Root)) {
    throw "Root path not found: $Root"
}

# Collect all .cs files excluding bin/obj directories and designer files
$files = Get-ChildItem -Path $Root -Recurse -Include *.cs -File |
    Where-Object {
        $_.FullName -notmatch "(\\|/)(bin|obj)(\\|/)" -and
        $_.Name -notmatch '(?i)\.designer\.cs$'
    }

# Regex to approximate public/private method declarations (excluding types/records and constructors)
$pub = '(?m)^\s*public\s+(?!class\b|struct\b|record\b|interface\b|enum\b)(?:async\s+)?(?:static\s+)?(?:unsafe\s+)?(?:readonly\s+)?(?:ref\s+)?(?:partial\s+)?[\w<>,\[\]\?\s]+\s+[\w\.]+\s*\([^;{)]*\)\s*(?:where\b.*)?\s*(?:\{|=>)'
$priv = '(?m)^\s*private\s+(?!protected\b)(?!class\b|struct\b|record\b|interface\b|enum\b)(?:async\s+)?(?:static\s+)?(?:unsafe\s+)?(?:readonly\s+)?(?:ref\s+)?(?:partial\s+)?[\w<>,\[\]\?\s]+\s+[\w\.]+\s*\([^;{)]*\)\s*(?:where\b.*)?\s*(?:\{|=>)'

$totalLines = 0
$publicMethods = 0
$privateMethods = 0
$processed = 0
$skipped = 0

foreach ($f in $files) {
    try {
        $c = Get-Content -LiteralPath $f.FullName -Raw -ErrorAction Stop
    } catch {
        $skipped++
        continue
    }

    $lc = 0
    if ($null -ne $c -and $c.Length -gt 0) {
        $lc = ([regex]::Matches($c, "\r?\n")).Count + 1
        $publicMethods += ([regex]::Matches($c, $pub)).Count
        $privateMethods += ([regex]::Matches($c, $priv)).Count
    }
    $totalLines += $lc
    $processed++
}

# Output
[pscustomobject]@{
    Root                   = (Resolve-Path -LiteralPath $Root).Path
    CsFilesFound           = $files.Count
    FilesProcessed         = $processed
    FilesSkipped           = $skipped
    TotalLines             = $totalLines
    PublicMethods_Approx   = $publicMethods
    PrivateMethods_Approx  = $privateMethods
} | Format-List
