# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

New-Item -ItemType Directory -Path (Join-Path $repoRoot 'out') -Force | Out-Null
& (Join-Path $repoRoot 'scripts\Test-ExperimentalRisk.ps1')
if (-not $?) { throw 'Experimental-risk gate tests failed.' }

$scriptParseFailures = @()
@(
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'driver') -Filter '*.ps1'
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'scripts') -Filter '*.ps1'
) | ForEach-Object {
        $tokens = $null
        $parseErrors = $null
        [Management.Automation.Language.Parser]::ParseFile(
            $_.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
        foreach ($parseError in $parseErrors) {
            $scriptParseFailures += "$($_.Name): $($parseError.Message)"
        }
    }
if ($scriptParseFailures.Count -ne 0) {
    throw ($scriptParseFailures -join [Environment]::NewLine)
}
& (Join-Path $repoRoot 'scripts\Test-Usb2XchangeFlexColorWorkflow.ps1')
if (-not $?) {
    throw 'Offline FlexColor operator workflow tests failed.'
}
& (Join-Path $repoRoot 'scripts\New-Usb2XchangeSourceRelease.ps1') `
    -AuditOnly -AllowDirty
if ($LASTEXITCODE -ne 0) {
    throw 'Source-release audit failed.'
}
. (Join-Path $repoRoot 'driver\Usb2Xchange-TestSupport.ps1')
if (-not (Test-Usb2XchangeTestSigningEnabled `
        -BootConfiguration @('testsigning             Yes')) -or
    -not (Test-Usb2XchangeTestSigningEnabled `
        -BootConfiguration @('', 'testsigning             Yes', '')) -or
    (Test-Usb2XchangeTestSigningEnabled `
        -BootConfiguration @('testsigning             No'))) {
    throw 'TESTSIGNING boot-configuration parser failed.'
}
$stringEku = [pscustomobject]@{ ObjectId = '1.3.6.1.5.5.7.3.3' }
$oidEku = [pscustomobject]@{
    ObjectId = [Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3')
}
$wrongEku = [pscustomobject]@{ ObjectId = '1.3.6.1.5.5.7.3.1' }
if (-not (Test-Usb2XchangeCodeSigningEku `
        -EnhancedKeyUsageList @($stringEku)) -or
    -not (Test-Usb2XchangeCodeSigningEku `
        -EnhancedKeyUsageList @($oidEku)) -or
    (Test-Usb2XchangeCodeSigningEku `
        -EnhancedKeyUsageList @($wrongEku))) {
    throw 'Code-signing EKU parser failed.'
}
Get-Usb2XchangeToolPaths | Out-Null
$flushTestPath = Join-Path $repoRoot 'out\test-support-flush.tmp'
Set-Content -LiteralPath $flushTestPath -Value 'durable-test' -Encoding Ascii
Sync-Usb2XchangeFile -Path $flushTestPath
if ((Get-Content -LiteralPath $flushTestPath -Raw).Trim() -ne 'durable-test') {
    throw 'Durable file flush helper changed file contents.'
}
Remove-Item -LiteralPath $flushTestPath -Force
$presentOnlyScripts = @(
    'Audit-Usb2XchangeVM.ps1',
    'Install-Usb2XchangeVMiniport.ps1'
)
foreach ($scriptName in $presentOnlyScripts) {
    $scriptText = Get-Content -LiteralPath (
        Join-Path $repoRoot "driver\$scriptName") -Raw
    if ($scriptText -notmatch 'Get-PnpDevice\s+-PresentOnly') {
        throw "$scriptName must ignore disconnected USB device records."
    }
}
$auditScriptText = Get-Content -LiteralPath (
    Join-Path $repoRoot 'driver\Audit-Usb2XchangeVM.ps1') -Raw
if ($auditScriptText -notmatch '\[switch\]\$PostSigning' -or
    $auditScriptText -notmatch 'the required reboot has not occurred') {
    throw 'Post-signing audit guard is missing.'
}
$installScriptText = Get-Content -LiteralPath (
    Join-Path $repoRoot 'driver\Install-Usb2XchangeVMiniport.ps1') -Raw
if ($installScriptText -match '\[ulong\]' -or
    $installScriptText -notmatch '\[uint64\]\$utcNow\.Ticks') {
    throw 'Offline generation must use a PowerShell 5.1-compatible UInt64 type.'
}
$enableScriptText = Get-Content -LiteralPath (
    Join-Path $repoRoot 'driver\Enable-Usb2XchangeTestSigning.ps1') -Raw
if ($enableScriptText -notmatch
        'Sync-Usb2XchangeFile\s+-Path\s+\$bootTimePath') {
    throw 'Pre-reboot boot marker is not flushed durably.'
}
$miniportSourceText = Get-Content -LiteralPath (
    Join-Path $repoRoot `
        'driver\Usb2Xchange.VMiniport\usb2xchange_vminiport.c') -Raw
$aspiTransportSourceText = Get-Content -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.AspiShim\AspiTransport.cs') -Raw
$winUsbDeviceSourceText = Get-Content -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.WinUsb\Usb2XchangeDevice.cs') -Raw
$operatorWorkflowText = Get-Content -LiteralPath (
    Join-Path $repoRoot 'scripts\Usb2Xchange-FlexColor.ps1') -Raw
$operatorPrepareText = Get-Content -LiteralPath (
    Join-Path $repoRoot 'scripts\Prepare-FlexColorAspiTest.ps1') -Raw
foreach ($requiredLegacyRuntime in @('MFC71.dll', 'MSVCR71.dll', 'MSVCP71.dll')) {
    if ($operatorWorkflowText -notmatch [regex]::Escape($requiredLegacyRuntime) -or
        $operatorPrepareText -notmatch [regex]::Escape($requiredLegacyRuntime)) {
        throw "Operator legacy-runtime prerequisite is missing: $requiredLegacyRuntime"
    }
}
if ($operatorWorkflowText -notmatch
        '\$legacyRuntimeState\s+-eq\s+''Supported''' -or
    $operatorPrepareText -notmatch 'Test-ExactLegacyRuntimeRoot' -or
    $operatorPrepareText -notmatch 'Private legacy runtime staging failed') {
    throw 'Operator VC++ 7.1 staging or ready-state guard is incomplete.'
}
if ($aspiTransportSourceText -notmatch
        'ExecuteOnePredictedPreviewImageRead[\s\S]{0,1600}ReadPrecisionTwoOperationalPreviewFirstImageOnce' -or
    $aspiTransportSourceText -match
        'ExecuteOnePredictedPreviewImageRead[\s\S]{0,800}device\.ExecuteReadOnly' -or
    $winUsbDeviceSourceText -notmatch
        'ReadPrecisionTwoOperationalPreviewFirstImageOnce[\s\S]{0,1200}\+\+precisionTwoPreviewImageReadsAttempted') {
    throw 'The one-row path must use its dedicated consume-before-USB WinUSB method.'
}
if ($aspiTransportSourceText -notmatch
        'ExecuteBoundedPreviewImageBurstRead[\s\S]{0,1800}ReadPrecisionTwoOperationalPreviewImageBurstRow' -or
    $aspiTransportSourceText -notmatch
        'ExecuteBoundedPreviewImageBurstRead[\s\S]{0,2200}RawStatus[\s\S]{0,500}ActualLength[\s\S]{0,800}\+\+livePreviewImageRowsCompleted' -or
    $aspiTransportSourceText -match
        'ExecuteBoundedPreviewImageBurstRead[\s\S]{0,1000}device\.ExecuteReadOnly' -or
    $winUsbDeviceSourceText -notmatch
        'ReadPrecisionTwoOperationalPreviewImageBurstRow[\s\S]{0,1800}\+\+precisionTwoPreviewImageReadsAttempted[\s\S]{0,300}ExecuteAcceptedCommand') {
    throw 'The eight-row path must use its dedicated ordered consume-before-USB WinUSB method.'
}
if ($miniportSourceText -notmatch
        'InitiatorBusId\[0\]\s*=\s*USB2XCHANGE_INITIATOR_ID') {
    throw 'Virtual SCSI initiator ID is not configured.'
}
if ($miniportSourceText -notmatch
        'initializationData\.NeedPhysicalAddresses\s*=\s*TRUE' -or
    $miniportSourceText -notmatch
        'initializationData\.AddressTypeFlags\s*=\s*ADDRESS_TYPE_FLAG_BTL8') {
    throw 'Storport initialization requirements are not configured.'
}
if ($miniportSourceText -match
        'ConfigInfo->(MapBuffers|NeedPhysicalAddresses|TaggedQueuing|AutoRequestSense|MultipleRequestPerLu)\s*=') {
    throw 'HwFindAdapter must not overwrite Storport-owned configuration fields.'
}
$brokerHeaderText = Get-Content -LiteralPath (
    Join-Path $repoRoot 'driver\shared\usb2xchange_broker_protocol.h') -Raw
if ($brokerHeaderText -notmatch
        'USB2X_MAX_TIMEOUT_MS\s+60000U' -or
    $brokerHeaderText -notmatch
        'USB2X_MAX_SRB_TIMEOUT_SECONDS\s+600U' -or
    $miniportSourceText -notmatch
        'TimeOutValue\s*>\s*USB2X_MAX_SRB_TIMEOUT_SECONDS' -or
    $miniportSourceText -notmatch
        'timeoutMilliseconds\s*=\s*USB2X_MAX_TIMEOUT_MS') {
    throw 'The miniport must accept the inbox 600-second SRB value and clamp broker work to 60 seconds.'
}
if ($miniportSourceText -notmatch
        'CdbLength\s*>\s*USB2X_MAX_CDB_LENGTH' -or
    $miniportSourceText -match
        'case\s+SCSIOP_(TEST_UNIT_READY|REQUEST_SENSE|INQUIRY)') {
    throw 'The miniport must publish bounded CDB metadata and leave the hardware allowlist to user mode.'
}
$miniportInfText = Get-Content -LiteralPath (
    Join-Path $repoRoot `
        'driver\Usb2Xchange.VMiniport\usb2xchange-vminiport.inf') -Raw
if ($miniportInfText -notmatch
        '(?m)^\s*LoadOrderGroup\s*=\s*SCSI Miniport\s*$' -or
    $miniportInfText -notmatch
        '(?m)^\s*HKR,Parameters,BusType,0x00010001,0x0000000E\s*$') {
    throw 'Virtual Storport INF service configuration is incomplete.'
}
if ($miniportInfText -notmatch
        '(?m)^\s*DriverVer\s*=\s*08/07/2026,0\.1\.0\.7\s*$') {
    throw 'Virtual Storport package revision is not 0.1.0.7.'
}
Write-Output 'PASS guarded driver-test script syntax and pure helpers'

$cleanPreservationFiles = @(
    (Join-Path $repoRoot 'out\driver\test-package\build-clean-preservation.test'),
    (Join-Path $repoRoot 'out\driver\test-signing\build-clean-preservation.test')
)
foreach ($preservationFile in $cleanPreservationFiles) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $preservationFile) `
        -Force | Out-Null
    Set-Content -LiteralPath $preservationFile -Value 'preserve' -Encoding Ascii
}
& (Join-Path $repoRoot 'build.ps1') -Clean
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
& (Join-Path $repoRoot 'scripts\Test-Usb2XchangeEndUser.ps1')
if (-not $?) {
    throw 'End-user package workflow tests failed.'
}
$windowsPowerShell = Join-Path $env:WINDIR `
    'System32\WindowsPowerShell\v1.0\powershell.exe'
& $windowsPowerShell -NoProfile -ExecutionPolicy Bypass `
    -File (Join-Path $repoRoot `
        'scripts\Test-Usb2XchangeFlexColorWorkflow.ps1') `
    -NativeCaptureOnly
if ($LASTEXITCODE -ne 0) {
    throw 'Windows PowerShell 5.1 native stderr capture regression failed.'
}
& $windowsPowerShell -NoProfile -ExecutionPolicy Bypass `
    -File (Join-Path $repoRoot 'scripts\Test-Usb2XchangeEndUser.ps1')
if ($LASTEXITCODE -ne 0) {
    throw 'Windows PowerShell 5.1 end-user workflow regression failed.'
}
foreach ($preservationFile in $cleanPreservationFiles) {
    if ((Get-Content -LiteralPath $preservationFile -Raw).Trim() -ne
            'preserve') {
        throw "Clean build removed protected signing state: $preservationFile"
    }
    Remove-Item -LiteralPath $preservationFile -Force
}

$demoTest = Start-Process -FilePath (Join-Path $repoRoot 'out\bin\usb2xchange-manager.exe') -ArgumentList '--demo-self-test' -WindowStyle Hidden -Wait -PassThru
if ($demoTest.ExitCode -ne 0) { throw "Manager demo self-test failed: $($demoTest.ExitCode)" }
Write-Output 'Manager demo: fail-closed routing and complete two-identity lifecycle passed.'

$testExecutable = Join-Path $repoRoot 'out\bin\Usb2Xchange.Tests.exe'
& $testExecutable
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$flexColorPatchTestExecutable = Join-Path $repoRoot `
    'out\bin\Usb2Xchange.FlexColorPatch.Tests.exe'
& $flexColorPatchTestExecutable
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$aspiTestExecutable = Join-Path $repoRoot 'out\aspi\Usb2Xchange.AspiShim.Tests.exe'
& $aspiTestExecutable
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$aspiProbeExecutable = Join-Path $repoRoot `
    'out\aspi\Usb2Xchange.AspiProbe.exe'
& $aspiProbeExecutable disabled
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
& $aspiProbeExecutable offline-replay
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$brokerTestExecutable = Join-Path $repoRoot 'out\bin\Usb2Xchange.BrokerProtocol.Tests.exe'
& $brokerTestExecutable
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$brokerIntegrationTestExecutable = Join-Path $repoRoot 'out\bin\Usb2Xchange.Broker.Tests.exe'
& $brokerIntegrationTestExecutable
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$brokerExecutable = Join-Path $repoRoot 'out\bin\usb2xchange-broker.exe'
& $brokerExecutable dry-run
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $repoRoot 'scripts\New-Usb2XchangeBinaryRelease.ps1') `
    -AuditOnly -AllowDirty
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$scsiScanProbeExecutable = Join-Path $repoRoot `
    'out\aspi\Usb2Xchange.ScsiScanProbe.exe'
& $scsiScanProbeExecutable dry-run
exit $LASTEXITCODE
