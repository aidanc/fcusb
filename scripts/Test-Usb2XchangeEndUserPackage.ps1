# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,

    [Parameter(Mandatory = $true)]
    [string]$AdapterFirmwarePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))).
    TrimEnd('\')
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'out')).TrimEnd('\')
$archive = [IO.Path]::GetFullPath($ArchivePath)
$source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
$firmware = [IO.Path]::GetFullPath($AdapterFirmwarePath)
$testId = [Guid]::NewGuid().ToString('N')
$extract = Join-Path $outputRoot "end-user-extract-smoke-$testId"
$user = Join-Path $outputRoot "end-user-install-smoke-$testId"

function Assert-SmokePath {
    param(
        [string]$Path,
        [string]$LeafPrefix
    )

    $resolved = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $parent = Split-Path -Parent $resolved
    $leaf = Split-Path -Leaf $resolved
    if (-not $parent.Equals($outputRoot,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not $leaf.StartsWith($LeafPrefix,
            [StringComparison]::Ordinal)) {
        throw "Unsafe end-user smoke path: $resolved"
    }
    return $resolved
}

$extract = Assert-SmokePath $extract 'end-user-extract-smoke-'
$user = Assert-SmokePath $user 'end-user-install-smoke-'
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
    throw "End-user archive was not found: $archive"
}
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "FlexColor source was not found: $source"
}
if (-not (Test-Path -LiteralPath $firmware -PathType Leaf)) {
    throw "Adapter firmware was not found: $firmware"
}

try {
    Expand-Archive -LiteralPath $archive -DestinationPath $extract
    $roots = @(Get-ChildItem -LiteralPath $extract -Directory)
    if ($roots.Count -ne 1) {
        throw 'The end-user archive must contain exactly one root directory.'
    }
    $endUser = Join-Path $roots[0].FullName `
        'scripts\Usb2Xchange-EndUser.ps1'
    $manager = Join-Path $roots[0].FullName 'USB2Xchange.exe'
    if (-not (Test-Path -LiteralPath $endUser -PathType Leaf) -or
        -not (Test-Path -LiteralPath $manager -PathType Leaf)) {
        throw 'The end-user package entry points are missing.'
    }
    $selfTest = Start-Process -FilePath $manager `
        -ArgumentList @('--self-test') -Wait -PassThru -WindowStyle Hidden
    if ($selfTest.ExitCode -ne 0) {
        throw 'The packaged manager failed its self-test.'
    }

    & $endUser -Action Install -SourceRoot $source `
        -AdapterFirmwarePath $firmware -UserRoot $user `
        -NoShellIntegration | Out-Null
    $status = (& $endUser -Action Status -UserRoot $user -Json) |
        ConvertFrom-Json
    if ($status.Installation -ne 'Installed' -or
        $status.PrivateState -ne 'Prepared' -or
        -not $status.ReadyToStart) {
        throw 'The packaged end-user installation was not ready.'
    }

    & $endUser -Action Repair -UserRoot $user -NoShellIntegration |
        Out-Null
    $installedEndUser = Join-Path $user `
        'App\scripts\Usb2Xchange-EndUser.ps1'
    & $installedEndUser -Action Uninstall -UserRoot $user `
        -NoShellIntegration |
        Out-Null
    if (Test-Path -LiteralPath $user) {
        throw 'The packaged end-user uninstall left its private user root.'
    }

    Write-Output 'PASS packaged end-user install/repair/status/uninstall smoke'
}
finally {
    foreach ($path in @($user, $extract)) {
        if (Test-Path -LiteralPath $path) {
            if ($path -eq $user) {
                Assert-SmokePath $path 'end-user-install-smoke-' | Out-Null
            }
            else {
                Assert-SmokePath $path 'end-user-extract-smoke-' | Out-Null
            }
            $removed = $false
            for ($attempt = 0; $attempt -lt 20 -and -not $removed; ++$attempt) {
                try {
                    Remove-Item -LiteralPath $path -Recurse -Force
                    $removed = $true
                }
                catch {
                    if ($attempt -eq 19) {
                        throw
                    }
                    Start-Sleep -Milliseconds 100
                }
            }
        }
    }
}
