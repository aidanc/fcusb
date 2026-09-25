# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Usb2Xchange-TestSupport.ps1')

Assert-Usb2XchangeAdministrator
$tools = Get-Usb2XchangeToolPaths
$repoRoot = Get-Usb2XchangeRepoRoot
$signingState = Get-Usb2XchangeSigningStateRoot
$testPackage = Join-Path $repoRoot 'out\driver\test-package'
$testInf = Join-Path $testPackage 'usb2xchange-vminiport.inf'
$testSys = Join-Path $testPackage 'usb2xchange-vminiport.sys'
$testCat = Join-Path $testPackage 'usb2xchange-vminiport.cat'
$bootTimePath = Join-Path $signingState 'pre-reboot-boot-time.txt'
$broker = Join-Path $repoRoot 'out\bin\usb2xchange-broker.exe'

foreach ($path in @($testInf, $testSys, $testCat, $bootTimePath, $broker)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Installation prerequisite is missing: $path"
    }
}
if ((Get-AuthenticodeSignature -LiteralPath $testSys).Status -ne 'Valid' -or
    (Get-AuthenticodeSignature -LiteralPath $testCat).Status -ne 'Valid') {
    throw 'The test package signatures are no longer valid.'
}
$bootConfiguration = Get-Usb2XchangeBootConfiguration
if (-not (Test-Usb2XchangeTestSigningEnabled `
            -BootConfiguration $bootConfiguration)) {
    throw 'TESTSIGNING is not enabled in the active boot configuration.'
}
$previousBootTime = [long]((Get-Content -LiteralPath $bootTimePath -Raw).Trim())
$os = Get-CimInstance Win32_OperatingSystem
$currentBootTime = $os.LastBootUpTime.ToFileTimeUtc()
if ($currentBootTime -eq $previousBootTime) {
    throw 'The VM has not restarted since TESTSIGNING was enabled.'
}
if (@(Get-Usb2XchangeRootDevices).Count -ne 0) {
    throw 'A USB2Xchange root miniport already exists; refusing a duplicate install.'
}
$usbDevices = @(Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object {
        $_.InstanceId -like 'USB\VID_03F3&PID_2003\*'
    })
if ($usbDevices.Count -ne 1 -or $usbDevices[0].Status -ne 'OK') {
    throw 'Expected exactly one healthy operational PID-2003 USB2Xchange device.'
}

& $tools.DevCon install $testInf $script:Usb2XchangeRootHardwareId
$devconExit = $LASTEXITCODE
if ($devconExit -ne 0 -and $devconExit -ne 1) {
    throw "DevCon installation failed with exit code $devconExit."
}

$device = $null
for ($attempt = 0; $attempt -lt 10; ++$attempt) {
    $devices = @(Get-Usb2XchangeRootDevices)
    if ($devices.Count -eq 1) {
        $device = $devices[0]
        break
    }
    Start-Sleep -Seconds 1
}
if ($null -eq $device) {
    throw 'The root miniport did not appear after installation.'
}
if ($device.Status -ne 'OK') {
    throw "The root miniport appeared with status $($device.Status)."
}
$service = Get-PnpDeviceProperty -InstanceId $device.InstanceId `
    -KeyName 'DEVPKEY_Device_Service' -ErrorAction Stop
if ([string]$service.Data -ne 'usb2xchange-vminiport') {
    throw "The root device uses unexpected service $($service.Data)."
}

& $broker status
if ($LASTEXITCODE -ne 0) {
    throw 'The checkpoint broker could not locate the installed StoragePort interface.'
}
$utcNow = (Get-Date).ToUniversalTime()
$generation = [uint64]$utcNow.Ticks
& $broker offline $generation
if ($LASTEXITCODE -ne 0) {
    throw 'The miniport did not accept the offline adapter generation.'
}

Write-Output "Root device: $($device.InstanceId)"
Write-Output "Status: $($device.Status)"
Write-Output "Service: $($service.Data)"
Write-Output "Offline generation: $generation"
Write-Output 'OFFLINE INSTALL RESULT: miniport is loaded with no scanner target exposed.'
if ($devconExit -eq 1) {
    Write-Output 'DevCon also requested a restart.'
}
