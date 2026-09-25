# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))).
    TrimEnd('\')
$script = Join-Path $repoRoot 'scripts\Usb2Xchange-EndUser.ps1'
$manager = Join-Path $repoRoot 'out\bin\usb2xchange-manager.exe'
$testRoot = Join-Path $repoRoot (
    'out\end-user-status-test-' + [Guid]::NewGuid().ToString('N'))

try {
    $json = & $script -Action Status -UserRoot $testRoot -Json
    if ($null -eq $json) {
        throw 'End-user Status returned no JSON.'
    }
    $status = $json | ConvertFrom-Json
    if ($status.Schema -ne 1 -or
        $status.Installation -ne 'NotInstalled' -or
        $status.ReadyToStart -ne $false -or
        (Test-Path -LiteralPath $testRoot)) {
        throw 'End-user Status is not side-effect-free before installation.'
    }

    $unsafeRejected = $false
    try {
        & $script -Action Status `
            -UserRoot ([IO.Path]::GetPathRoot($repoRoot)) | Out-Null
    }
    catch {
        $unsafeRejected = $true
    }
    if (-not $unsafeRejected) {
        throw 'End-user workflow accepted an unsafe filesystem root.'
    }

    if (-not (Test-Path -LiteralPath $manager -PathType Leaf)) {
        throw 'The built end-user manager is missing.'
    }
    $selfTest = Start-Process -FilePath $manager `
        -ArgumentList @('--self-test') -Wait -PassThru -WindowStyle Hidden
    if ($selfTest.ExitCode -ne 0) {
        throw 'The built end-user manager failed its self-test.'
    }

    $source = Get-Content -Raw -LiteralPath $script
    foreach ($required in @(
            'Get-PackageManifest',
            'RegisterPresentInterface',
            'InitializeAdapter',
            'New-EndUserShortcuts',
            'Remove-EndUserShortcuts',
            'WaitForProcessId')) {
        if ($source -notmatch [regex]::Escape($required)) {
            throw "End-user workflow is missing: $required"
        }
    }
    if ($source -notmatch [regex]::Escape(
            'D0967EF81E71E9293D0499C91D687E2409F8CA13B14FFE2C4F35F07685D25FBD') -or
        $source -notmatch 'PackageType\s+-ne\s+''PortableUserModeRuntime''' -or
        $source -notmatch 'Remove-Item\s+-LiteralPath\s+\$user\s+-Recurse') {
        throw 'End-user package identity or exact uninstall guard is missing.'
    }

    Write-Output 'PASS end-user manager and side-effect-free preinstall status'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        $resolved = [IO.Path]::GetFullPath($testRoot)
        if ($resolved.StartsWith(
                ([IO.Path]::GetFullPath((Join-Path $repoRoot 'out')).
                    TrimEnd('\') + '\end-user-status-test-'),
                [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
