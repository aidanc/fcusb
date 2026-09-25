# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [switch]$NativeCaptureOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$workflow = Join-Path $repoRoot 'scripts\Usb2Xchange-FlexColor.ps1'
$testRoot = Join-Path $repoRoot (
    'out\workflow-tests\' + [Guid]::NewGuid().ToString('N'))

if ($NativeCaptureOnly) {
    # Import the workflow functions without creating the missing private tree
    # or touching hardware, then exercise native stderr under this host's
    # PowerShell semantics.
    . $workflow -Action Status -PrivateRoot $testRoot -Json | Out-Null
    $windowsPowerShell = Join-Path $env:WINDIR `
        'System32\WindowsPowerShell\v1.0\powershell.exe'
    $capture = Invoke-CapturedNativeCommand -Executable $windowsPowerShell `
        -Arguments @(
            '-NoProfile',
            '-Command',
            "[Console]::Error.WriteLine('native-capture-ok'); exit 0")
    if ($capture.ExitCode -ne 0 -or
        ($capture.Output -join "`n") -notmatch 'native-capture-ok' -or
        $ErrorActionPreference -ne 'Stop') {
        throw 'Native stderr capture did not preserve output, exit, and policy.'
    }
    Write-Output (
        "PASS native stderr capture under PowerShell $($PSVersionTable.PSVersion)")
    exit 0
}

$json = & $workflow -Action Status -PrivateRoot $testRoot -Json
$status = $json | ConvertFrom-Json
if ($status.Schema -ne 1 -or $status.PrivateState -ne 'Missing' -or
    $status.AdapterFirmware -ne 'NotConfigured' -or
    $status.ScannerFirmware -ne 'Missing' -or
    $status.LegacyRuntime -notlike 'Missing:*' -or
    $status.Hardware -ne 'NotChecked' -or
    $status.OperatorRuntime -ne
        'TrustedFlexColorPassThroughHardwareAccepted' -or
    $status.ReadyForOperatorStart) {
    throw 'Offline missing-tree workflow status is incorrect.'
}
if (Test-Path -LiteralPath $testRoot) {
    throw 'Read-only workflow status created the missing private root.'
}

$startRefused = $false
try {
    & $workflow -Action Start -PrivateRoot $testRoot
}
catch {
    $startRefused = $_.Exception.Message -match
        'private FlexColor copy is not prepared'
}
if (-not $startRefused -or (Test-Path -LiteralPath $testRoot)) {
    throw 'Operator Start did not fail closed on a missing private tree.'
}

$missingAdapterFirmware = $testRoot + '-missing-adapter.fw'
$firmwareJson = & $workflow -Action Status -PrivateRoot $testRoot `
    -AdapterFirmwarePath $missingAdapterFirmware -Json
$firmwareStatus = $firmwareJson | ConvertFrom-Json
if ($firmwareStatus.AdapterFirmware -ne 'Missing' -or
    $firmwareStatus.AdapterFirmwarePath -ne $missingAdapterFirmware -or
    (Test-Path -LiteralPath $missingAdapterFirmware)) {
    throw 'Adapter-firmware status did not report a missing input read-only.'
}

$reparseTarget = $testRoot + '-target'
$reparsePrivate = $testRoot + '-junction'
New-Item -ItemType Directory -Path $reparseTarget -Force | Out-Null
New-Item -ItemType Junction -Path $reparsePrivate -Target $reparseTarget |
    Out-Null
$reparseRefused = $false
try {
    & $workflow -Action Status -PrivateRoot $reparsePrivate -Json
}
catch {
    $reparseRefused = $_.Exception.Message -match
        'PrivateRoot contains a reparse point'
}
if (-not $reparseRefused) {
    throw 'Workflow status did not reject a reparse-point private root.'
}
Remove-Item -LiteralPath $reparsePrivate -Force
if (-not (Test-Path -LiteralPath $reparseTarget -PathType Container)) {
    throw 'Removing the test junction affected its target.'
}
Remove-Item -LiteralPath $reparseTarget -Force

New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$sessionPath = Join-Path $testRoot '.usb2xchange-operator-session.json'
[pscustomobject][ordered]@{
    Schema = 1
    ProcessId = $PID
    Executable = Join-Path $testRoot 'FlexColor.exe'
    StartTimeUtc = (Get-Process -Id $PID).StartTime.ToUniversalTime().
        ToString('O')
} | ConvertTo-Json | Set-Content -LiteralPath $sessionPath -Encoding UTF8

$ownershipMismatchRefused = $false
try {
    & $workflow -Action Deactivate -PrivateRoot $testRoot
}
catch {
    $ownershipMismatchRefused = $_.Exception.Message -match
        'managed-session record cannot be trusted'
}
if (-not $ownershipMismatchRefused -or
    -not (Test-Path -LiteralPath $sessionPath)) {
    throw 'Deactivation did not refuse a mismatched live process record.'
}

[pscustomobject][ordered]@{
    Schema = 1
    ProcessId = [int]::MaxValue
    Executable = Join-Path $testRoot 'FlexColor.exe'
    StartTimeUtc = [DateTimeOffset]::UtcNow.ToString('O')
} | ConvertTo-Json | Set-Content -LiteralPath $sessionPath -Encoding UTF8

$stopOutput = & $workflow -Action Stop -PrivateRoot $testRoot
if ($stopOutput -notmatch 'stale managed-session record' -or
    (Test-Path -LiteralPath $sessionPath)) {
    throw 'Safe stale-session cleanup did not complete.'
}

# Import the workflow functions into this disposable test process, then replace
# only its PnP query with synthetic objects. No adapter is enumerated or opened.
. $workflow -Action Status -PrivateRoot $testRoot -Json | Out-Null

$roundTripTimestamp = (
    '{"StartTimeUtc":"2026-08-21T09:55:20.3503982Z"}' |
        ConvertFrom-Json).StartTimeUtc
$expectedTimestamp = [DateTimeOffset]::Parse(
    '2026-08-21T09:55:20.3503982Z',
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::RoundtripKind).UtcDateTime
$actualTimestamp = Convert-SessionTimeToUtc $roundTripTimestamp
$stringTimestamp = Convert-SessionTimeToUtc (
    '2026-08-21T09:55:20.3503982Z')
if ($actualTimestamp.Ticks -ne $expectedTimestamp.Ticks -or
    $stringTimestamp.Ticks -ne $expectedTimestamp.Ticks) {
    throw (
        'Session timestamp conversion was not stable across JSON DateTime ' +
        'and string representations.')
}

function Get-PresentUsb2XchangeDevices {
    return @([pscustomobject]@{
            InstanceId = 'USB\VID_03F3&PID_2003\TEST'
            Status = 'OK'
        })
}
$operationalOutput = @(Require-OperationalAdapter)
if ($operationalOutput -notmatch 'firmware upload skipped') {
    throw 'Synthetic healthy PID-2003 state did not skip firmware upload.'
}

function Get-PresentUsb2XchangeDevices {
    return @([pscustomobject]@{
            InstanceId = 'USB\VID_03F3&PID_2002\TEST'
            Status = 'OK'
        })
}
$AdapterFirmwarePath = $null
$loaderWithoutFirmwareRefused = $false
try {
    Require-OperationalAdapter
}
catch {
    $loaderWithoutFirmwareRefused = $_.Exception.Message -match
        'loader PID 2002.*AdapterFirmwarePath'
}
if (-not $loaderWithoutFirmwareRefused) {
    throw 'Synthetic PID-2002 state did not require exact local firmware.'
}

function Get-PresentUsb2XchangeDevices { return @() }
$missingDeviceRefused = $false
try {
    Require-OperationalAdapter
}
catch {
    $missingDeviceRefused = $_.Exception.Message -match
        'USB2Xchange was not found'
}
if (-not $missingDeviceRefused) {
    throw 'Synthetic missing-adapter state did not fail closed.'
}

function Get-PresentUsb2XchangeDevices {
    return @(
        [pscustomobject]@{
            InstanceId = 'USB\VID_03F3&PID_2002\TEST'
            Status = 'OK'
        },
        [pscustomobject]@{
            InstanceId = 'USB\VID_03F3&PID_2003\TEST'
            Status = 'OK'
        })
}
$ambiguousDeviceRefused = $false
try {
    Require-OperationalAdapter
}
catch {
    $ambiguousDeviceRefused = $_.Exception.Message -match
        'enumeration is ambiguous'
}
if (-not $ambiguousDeviceRefused) {
    throw 'Synthetic ambiguous adapter state did not fail closed.'
}

function Get-PresentUsb2XchangeDevices {
    return @([pscustomobject]@{
            InstanceId = 'USB\VID_03F3&PID_2003\TEST'
            Status = 'Error'
        })
}
$unhealthyDeviceRefused = $false
try {
    Require-OperationalAdapter
}
catch {
    $unhealthyDeviceRefused = $_.Exception.Message -match
        'PID 2003 is present but not healthy'
}
if (-not $unhealthyDeviceRefused) {
    throw 'Synthetic unhealthy PID-2003 state did not fail closed.'
}

$m333Diagnostic = @(
    'Target 5, LUN 0 responded:',
    'Vendor: Imacon',
    'Product: FlexTight II',
    'Revision: M333',
    'Responding targets: 1')
if ((Get-SupportedScannerIdentityFromDiagnostic -ExitCode 0 `
        -Output $m333Diagnostic) -ne 'Imacon / FlexTight II / M333') {
    throw 'Synthetic M333 diagnostic did not classify exactly.'
}
$l302Diagnostic = @(
    'Target 5, LUN 0 responded:',
    'Vendor: Imacon',
    'Product: SCSI Loader',
    'Revision: L302',
    'Responding targets: 1')
if ((Get-SupportedScannerIdentityFromDiagnostic -ExitCode 0 `
        -Output $l302Diagnostic) -ne 'Imacon / SCSI Loader / L302') {
    throw 'Synthetic L302 diagnostic did not classify exactly.'
}
$wrongTargetDiagnostic = $m333Diagnostic.Clone()
$wrongTargetDiagnostic[0] = 'Target 4, LUN 0 responded:'
if ($null -ne (Get-SupportedScannerIdentityFromDiagnostic -ExitCode 0 `
        -Output $wrongTargetDiagnostic)) {
    throw 'Synthetic wrong-target identity was accepted.'
}
$multipleTargetDiagnostic = $m333Diagnostic.Clone()
$multipleTargetDiagnostic[4] = 'Responding targets: 2'
if ($null -ne (Get-SupportedScannerIdentityFromDiagnostic -ExitCode 0 `
        -Output $multipleTargetDiagnostic)) {
    throw 'Synthetic multi-target identity was accepted.'
}
$mixedIdentityDiagnostic = $m333Diagnostic.Clone()
$mixedIdentityDiagnostic[2] = 'Product: SCSI Loader'
if ($null -ne (Get-SupportedScannerIdentityFromDiagnostic -ExitCode 0 `
        -Output $mixedIdentityDiagnostic)) {
    throw 'Synthetic mixed product/revision identity was accepted.'
}

$launcherOutput = @(
    'Private root: C:\private',
    'Started trusted transparent FlexColor PID 1234.',
    'ASPI log: C:\private\Usb2XchangeLogs\accepted.log')
$launcherRecord = Get-TrustedLauncherRecordFromOutput -ExitCode 0 `
    -Output $launcherOutput
if ($null -eq $launcherRecord -or $launcherRecord.ProcessId -ne 1234 -or
    $launcherRecord.LogPath -ne
        'C:\private\Usb2XchangeLogs\accepted.log') {
    throw 'Synthetic exact trusted launcher output did not classify.'
}
$duplicateLauncherOutput = @($launcherOutput +
    'Started trusted transparent FlexColor PID 5678.')
if ($null -ne (Get-TrustedLauncherRecordFromOutput -ExitCode 0 `
        -Output $duplicateLauncherOutput)) {
    throw 'Synthetic ambiguous trusted launcher output was accepted.'
}
if ($null -ne (Get-TrustedLauncherRecordFromOutput -ExitCode 2 `
        -Output $launcherOutput)) {
    throw 'Synthetic failed trusted launcher output was accepted.'
}

Remove-Item -LiteralPath $testRoot -Force
Write-Output (
    'PASS offline FlexColor operator workflow status/stop/hardware guards')
