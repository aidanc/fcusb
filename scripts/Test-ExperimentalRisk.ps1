# Copyright (c) 2026 fcusb contributors
# SPDX-License-Identifier: GPL-3.0-only
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$script = Join-Path $root 'scripts\Usb2Xchange-EndUser.ps1'
$output = [IO.Path]::GetFullPath((Join-Path $root 'out')).TrimEnd('\')
$testRoot = Join-Path $output ('risk-test-' + [Guid]::NewGuid().ToString('N'))
try {
    # Each action must fail at acknowledgement before config, PnP or USB dispatch.
    foreach ($action in @('Start', 'InitializeAdapter', 'RegisterPresentInterface')) {
        $rejected = $false
        try { & $script -Action $action -UserRoot $testRoot | Out-Null }
        catch {
            if ($_.Exception.Message -notlike '*risk acknowledgement required*') {
                throw
            }
            $rejected = $true
        }
        if (-not $rejected -or (Test-Path -LiteralPath $testRoot)) {
            throw "Unacknowledged $action did not fail without side effects."
        }
    }
    & $script -Action AcknowledgeRisk -UserRoot $testRoot | Out-Null
    $marker = Join-Path $testRoot 'risk-acknowledgement.txt'
    if ([IO.File]::ReadAllText($marker) -cne 'fcusb-experimental-risk-v1' -or
        @(Get-ChildItem -LiteralPath $testRoot -Force).Count -ne 1) {
        throw 'Acknowledgement stored unexpected state.'
    }
    # Accepting permits normal validation, not skipping installation requirements.
    $missingInstall = $false
    try { & $script -Action Start -UserRoot $testRoot | Out-Null }
    catch {
        if ($_.Exception.Message -notlike '*not installed*') { throw }
        $missingInstall = $true
    }
    if (-not $missingInstall) { throw 'Acknowledgement bypassed installation validation.' }
    foreach ($invalid in @('', 'fcusb-experimental-risk-v0', 'FCUSB-EXPERIMENTAL-RISK-V1')) {
        [IO.File]::WriteAllText($marker, $invalid)
        $rejected = $false
        try { & $script -Action Start -UserRoot $testRoot | Out-Null }
        catch {
            if ($_.Exception.Message -notlike '*risk acknowledgement required*') { throw }
            $rejected = $true
        }
        if (-not $rejected) { throw 'Invalid warning marker was accepted.' }
    }
    Write-Output 'PASS experimental-risk gate: missing/refused, persisted, stale/corrupt, and independent installation guard; no hardware accessed'
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ($resolved.StartsWith($output + '\risk-test-',
        [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
