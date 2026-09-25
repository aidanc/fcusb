# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [string]$OutputDirectory,
    [switch]$AuditOnly,
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'out\release'
}
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'out')).TrimEnd('\')
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if (-not $output.StartsWith($outputRoot + '\',
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must remain under the ignored output tree: $outputRoot"
}
$outputAncestor = $output
while (-not [string]::IsNullOrEmpty($outputAncestor) -and
    $outputAncestor.StartsWith($outputRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
    if ((Test-Path -LiteralPath $outputAncestor) -and
        ((Get-Item -LiteralPath $outputAncestor).Attributes -band
            [IO.FileAttributes]::ReparsePoint)) {
        throw "OutputDirectory contains a reparse point: $outputAncestor"
    }
    $parent = [IO.Directory]::GetParent($outputAncestor)
    $outputAncestor = if ($null -eq $parent) { $null } else { $parent.FullName }
}

$git = (Get-Command git -ErrorAction Stop).Source
$actualRoot = (& $git -C $repoRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or
    -not ([IO.Path]::GetFullPath($actualRoot).TrimEnd('\') -eq
        $repoRoot.TrimEnd('\'))) {
    throw 'The script is not running from the expected Git worktree.'
}

if (-not $AllowDirty) {
    $changes = @(& $git -C $repoRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the Git worktree.'
    }
    if ($changes.Count -ne 0) {
        throw 'Refusing to stage a source release from a dirty worktree.'
    }
}

$tracked = @(& $git -C $repoRoot ls-files)
if ($LASTEXITCODE -ne 0 -or $tracked.Count -eq 0) {
    throw 'Unable to obtain the tracked source manifest.'
}

$forbiddenNames = @(
    'MICROCOD.3XX',
    'FlexColor.exe',
    'FlexColor.dll',
    'wnaspi32.dll'
)
$forbiddenExtensions = @(
    '.7z', '.bin', '.cab', '.cat', '.dll', '.dmp', '.etl', '.exe', '.fw',
    '.gpr', '.hex', '.i64', '.idb', '.img', '.log', '.msi', '.pcap',
    '.pcapng', '.pdb', '.rar', '.raw', '.rep', '.rom', '.sys', '.tif',
    '.tiff', '.trace', '.zip', '.fff', '.3fr', '.pdf', '.pml', '.mpd', '.pem', '.key', '.pfx'
)
$proprietaryHashes = @(
    '4C5D402F3668F06BEAFC871B9F152D55BF5ACCA191C43082C6A8C5647916AF28',
    'B49217BA2BBFF2E9A9DF0952CC9657CD197C10022E2B62E3B818719FB78C1E84',
    'D250B6177D2B30FADD55E06DF612E7F534CB5D1A30E82AA9CC71A3F1924DD119',
    'D0967EF81E71E9293D0499C91D687E2409F8CA13B14FFE2C4F35F07685D25FBD',
    'D8D7188574C52B255CF7940BEF7CC9192F744693AABC1F735758B7FECDEC65F3',
    '4DA5EFDC46D126B45DAEEE8BC69C0BA2AA243589046B7DFD12A7E21B9BEE6A32',
    '8094AF5EE310714CAEBCCAEEE7769FFB08048503BA478B879EDFEF5F1A24FEFE',
    'DF96156F6A548FD6FE5672918DE5AE4509D3C810A57BFFD2A91DE45A3ED5B23B',
    'FA2629BDF855B5A320D2C184B40FFB2B780D8FDB67491504CEF2C5AA0E3E8381'
)

$violations = New-Object Collections.Generic.List[string]
foreach ($relativePath in $tracked) {
    $normalized = $relativePath.Replace('\', '/')
    $leaf = [IO.Path]::GetFileName($normalized)
    $extension = [IO.Path]::GetExtension($leaf)
    if ($normalized -match '^(output|tmp|captures|dumps|reference|analysis)/') {
        $violations.Add("private/generated tree is tracked: $normalized")
    }
    if ($normalized.StartsWith('out/',
            [StringComparison]::OrdinalIgnoreCase)) {
        $violations.Add("ignored output is tracked: $normalized")
    }
    if ($normalized.StartsWith('analysis/',
            [StringComparison]::OrdinalIgnoreCase) -and
        -not $normalized.Equals('analysis/README.md',
            [StringComparison]::OrdinalIgnoreCase)) {
        $violations.Add("generated analysis is tracked: $normalized")
    }
    if ($normalized.StartsWith('reference/legacy/',
            [StringComparison]::OrdinalIgnoreCase) -and
        -not $normalized.Equals('reference/legacy/.gitkeep',
            [StringComparison]::OrdinalIgnoreCase)) {
        $violations.Add("legacy artifact is tracked: $normalized")
    }
    if ($forbiddenNames -contains $leaf) {
        $violations.Add("proprietary filename is tracked: $normalized")
    }
    if ($forbiddenExtensions -contains $extension.ToLowerInvariant()) {
        $violations.Add("forbidden release extension is tracked: $normalized")
    }

    $fullPath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        $violations.Add("tracked file is missing or not a regular file: $normalized")
        continue
    }
    if ((Get-Item -LiteralPath $fullPath).Attributes -band
        [IO.FileAttributes]::ReparsePoint) {
        $violations.Add("tracked file is a reparse point: $normalized")
        continue
    }
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash
    if ($proprietaryHashes -contains $hash) {
        $violations.Add("known proprietary content hash is tracked: $normalized")
    }
}

if ($violations.Count -ne 0) {
    throw ("Release audit failed:`r`n" + ($violations -join "`r`n"))
}

$commit = (& $git -C $repoRoot rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit.Length -ne 40) {
    throw 'Unable to resolve the release commit.'
}
Write-Host "Release source audit passed: $($tracked.Count) tracked files."
Write-Host "Commit: $commit"

if ($AuditOnly) {
    Write-Host 'Audit-only mode made no files.'
    exit 0
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$shortCommit = $commit.Substring(0, 12)
$baseName = "usb2xchange-source-$shortCommit"
$archivePath = Join-Path $output ($baseName + '.zip')
$manifestPath = Join-Path $output ($baseName + '.sha256.txt')
if ((Test-Path -LiteralPath $archivePath) -or
    (Test-Path -LiteralPath $manifestPath)) {
    throw "Refusing to overwrite an existing release artifact: $baseName"
}

& $git -C $repoRoot archive --format=zip `
    "--prefix=$baseName/" "--output=$archivePath" HEAD
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    throw 'git archive failed.'
}
$archiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash
$manifestLines = @(
    "commit=$commit",
    "source_file_count=$($tracked.Count)",
    "archive_sha256=$archiveHash",
    "archive_name=$([IO.Path]::GetFileName($archivePath))"
)
Set-Content -LiteralPath $manifestPath -Value $manifestLines -Encoding Ascii

Write-Host "Archive:  $archivePath"
Write-Host "SHA-256:  $archiveHash"
Write-Host "Manifest: $manifestPath"
