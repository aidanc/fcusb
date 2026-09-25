# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [string]$SourceRoot =
        'C:\Program Files (x86)\Hasselblad\FlexColor English v4.0.3',
    [string]$PrivateRoot,
    [string]$LegacyRuntimeRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ([string]::IsNullOrWhiteSpace($PrivateRoot)) {
    $PrivateRoot = Join-Path $repoRoot 'out\flexcolor-aspi-private'
}
$source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$private = [IO.Path]::GetFullPath($PrivateRoot).TrimEnd('\')
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'out')).TrimEnd('\')
if (-not $private.StartsWith($outputRoot + '\',
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "PrivateRoot must remain under the ignored output tree: $outputRoot"
}
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Source FlexColor tree was not found: $source"
}

$sourceExe = Join-Path $source 'FlexColor.exe'
$sourceDll = Join-Path $source 'DLLS\FlexColor.dll'
$expectedExe = '4C5D402F3668F06BEAFC871B9F152D55BF5ACCA191C43082C6A8C5647916AF28'
$expectedDll = 'B49217BA2BBFF2E9A9DF0952CC9657CD197C10022E2B62E3B818719FB78C1E84'
$legacyRuntimeFiles = [ordered]@{
    'MFC71.dll' =
        '4DA5EFDC46D126B45DAEEE8BC69C0BA2AA243589046B7DFD12A7E21B9BEE6A32'
    'MSVCR71.dll' =
        '8094AF5EE310714CAEBCCAEEE7769FFB08048503BA478B879EDFEF5F1A24FEFE'
    'MSVCP71.dll' =
        'DF96156F6A548FD6FE5672918DE5AE4509D3C810A57BFFD2A91DE45A3ED5B23B'
}
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $sourceExe).Hash -ne
        $expectedExe -or
    (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDll).Hash -ne
        $expectedDll) {
    throw 'The source is not the exact supported FlexColor 4.0.3 tree.'
}

function Test-ExactLegacyRuntimeRoot {
    param([string]$Root)

    if ([string]::IsNullOrWhiteSpace($Root) -or
        -not (Test-Path -LiteralPath $Root -PathType Container) -or
        ((Get-Item -LiteralPath $Root).Attributes -band
            [IO.FileAttributes]::ReparsePoint)) {
        return $false
    }
    foreach ($entry in $legacyRuntimeFiles.GetEnumerator()) {
        $path = Join-Path $Root $entry.Key
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            ((Get-Item -LiteralPath $path).Attributes -band
                [IO.FileAttributes]::ReparsePoint) -or
            (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash -ne
                $entry.Value) {
            return $false
        }
    }
    return $true
}

$legacyCandidates = New-Object Collections.Generic.List[string]
if (-not [string]::IsNullOrWhiteSpace($LegacyRuntimeRoot)) {
    $legacyCandidates.Add(
        [IO.Path]::GetFullPath($LegacyRuntimeRoot).TrimEnd('\'))
}
else {
    $legacyCandidates.Add($source)
    $legacyCandidates.Add((Join-Path $env:WINDIR 'SysWOW64'))
}
$legacySource = $null
foreach ($candidate in $legacyCandidates) {
    if (Test-ExactLegacyRuntimeRoot $candidate) {
        $legacySource = $candidate
        break
    }
}
if ($null -eq $legacySource) {
    throw (
        'The exact 32-bit Visual C++ 7.1 runtime required by FlexColor is ' +
        'missing. Supply -LegacyRuntimeRoot containing the supported ' +
        'MFC71.dll, MSVCR71.dll, and MSVCP71.dll files. These licensed ' +
        'inputs are not included in the project package.')
}

function Assert-PackagedRuntime {
    param([string]$ManifestPath)

    $manifest = Get-Content -Raw -LiteralPath $ManifestPath |
        ConvertFrom-Json
    if ($manifest.Schema -ne 1 -or
        $manifest.PackageType -ne 'PortableUserModeRuntime' -or
        [string]$manifest.Commit -notmatch '^[0-9a-f]{40}$' -or
        $null -eq $manifest.Files) {
        throw 'The portable runtime package manifest is invalid.'
    }
    $required = @(
        'out/bin/usb2xchange.exe',
        'out/bin/Usb2Xchange.WinUsb.dll',
        'out/bin/Usb2Xchange.Protocol.dll',
        'out/bin/flexcolor-aspi-patch.exe',
        'out/bin/flexcolor-aspi-launcher.exe',
        'out/aspi/wnaspi32.dll',
        'out/aspi/wnaspi32.dll.config',
        'out/aspi/Usb2Xchange.AspiShim.Managed.dll',
        'out/aspi/Usb2Xchange.WinUsb.dll',
        'out/aspi/Usb2Xchange.Protocol.dll',
        'out/aspi/FlexColor.exe.config',
        'out/aspi/Usb2Xchange.AspiProbe.exe',
        'scripts/Prepare-FlexColorAspiTest.ps1',
        'scripts/Usb2Xchange-FlexColor.ps1'
    )
    $seen = @{}
    foreach ($record in @($manifest.Files)) {
        $relative = ([string]$record.Path).Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($relative) -or
            [IO.Path]::IsPathRooted($relative) -or
            $relative.Contains(':') -or
            $relative -match '(^|/)\.\.(/|$)' -or
            [string]$record.Sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
            [int64]$record.Length -lt 0) {
            throw "Invalid portable runtime manifest record: $relative"
        }
        $key = $relative.ToLowerInvariant()
        if ($seen.ContainsKey($key)) {
            throw "Duplicate portable runtime manifest path: $relative"
        }
        $full = [IO.Path]::GetFullPath((Join-Path $repoRoot $relative))
        if (-not $full.StartsWith($repoRoot.TrimEnd('\') + '\',
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $full -PathType Leaf) -or
            ((Get-Item -LiteralPath $full).Attributes -band
                [IO.FileAttributes]::ReparsePoint) -or
            (Get-Item -LiteralPath $full).Length -ne [int64]$record.Length -or
            (Get-FileHash -Algorithm SHA256 -LiteralPath $full).Hash -ne
                ([string]$record.Sha256).ToUpperInvariant()) {
            throw "Portable runtime package file failed validation: $relative"
        }
        $seen[$key] = $true
    }
    foreach ($relative in $required) {
        if (-not $seen.ContainsKey($relative.ToLowerInvariant())) {
            throw "Portable runtime package is missing: $relative"
        }
    }
}
$packageManifest = Join-Path $repoRoot 'USB2XCHANGE-RUNTIME-PACKAGE.json'
if (Test-Path -LiteralPath $packageManifest -PathType Leaf) {
    Assert-PackagedRuntime -ManifestPath $packageManifest
    Write-Output 'Validated portable runtime package; local build was skipped.'
}
else {
    & (Join-Path $repoRoot 'scripts\Build-FlexColorAspiTest.ps1')
    & (Join-Path $repoRoot 'out\aspi\Usb2Xchange.AspiShim.Tests.exe')
    if ($LASTEXITCODE -ne 0) {
        throw 'ASPI provider tests failed.'
    }
}

& (Join-Path $repoRoot 'out\aspi\Usb2Xchange.AspiProbe.exe') disabled
if ($LASTEXITCODE -ne 0) {
    throw 'Native-export disabled-mode probe failed.'
}
& (Join-Path $repoRoot 'out\aspi\Usb2Xchange.AspiProbe.exe') `
    offline-replay
if ($LASTEXITCODE -ne 0) {
    throw 'Native-export offline replay probe failed.'
}

if (-not (Test-Path -LiteralPath $private)) {
    Copy-Item -LiteralPath $source -Destination $private -Recurse
}
$privateExe = Join-Path $private 'FlexColor.exe'
$privateDll = Join-Path $private 'DLLS\FlexColor.dll'
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $privateExe).Hash -ne
        $expectedExe) {
    throw 'Existing private FlexColor.exe does not match the supported image.'
}
foreach ($entry in $legacyRuntimeFiles.GetEnumerator()) {
    Copy-Item -LiteralPath (Join-Path $legacySource $entry.Key) `
        -Destination (Join-Path $private $entry.Key) -Force
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (
                Join-Path $private $entry.Key)).Hash -ne $entry.Value) {
        throw "Private legacy runtime staging failed: $($entry.Key)"
    }
}
Write-Output "Staged exact private VC++ 7.1 runtime from $legacySource."

$marker = Join-Path $private '.usb2xchange-aspi-staged'
$stageNames = @(
    'wnaspi32.dll',
    'wnaspi32.dll.config',
    'Usb2Xchange.AspiShim.Managed.dll',
    'Usb2Xchange.WinUsb.dll',
    'Usb2Xchange.Protocol.dll',
    'FlexColor.exe.config'
)
if (-not (Test-Path -LiteralPath $marker)) {
    foreach ($name in $stageNames) {
        if (Test-Path -LiteralPath (Join-Path $private $name)) {
            throw "Refusing to replace an unowned private-tree file: $name"
        }
    }
}
foreach ($name in $stageNames) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "out\aspi\$name") `
        -Destination (Join-Path $private $name) -Force
}
Set-Content -LiteralPath $marker -Encoding Ascii `
    -Value 'USB2Xchange ASPI private staging v1'

& (Join-Path $repoRoot 'out\bin\flexcolor-aspi-patch.exe') `
    activate $privateDll
if ($LASTEXITCODE -ne 0) {
    throw 'FlexColor private-copy patch activation failed.'
}
& (Join-Path $repoRoot 'out\bin\flexcolor-aspi-launcher.exe') `
    preflight $private
if ($LASTEXITCODE -ne 0) {
    throw 'FlexColor ASPI preflight failed.'
}

Write-Output 'Private offline-replay test tree is ready.'
Write-Output "Run: .\scripts\Start-FlexColorAspiTest.ps1 -Action Smoke"
