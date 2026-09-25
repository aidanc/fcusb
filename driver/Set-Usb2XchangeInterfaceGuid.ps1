# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [Parameter(Mandatory = $true)]
    [string]$InstanceId
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$interfaceGuid = '{86A64B6A-BC77-49D2-B378-0F43E5DAA568}'
$allowed = '^USB\\VID_03F3&PID_200[23]\\'

if ($InstanceId -notmatch $allowed) {
    throw 'InstanceId must be a USB2Xchange PID 2002 or PID 2003 instance.'
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell window.'
}

$device = Get-PnpDevice -InstanceId $InstanceId -ErrorAction Stop
$serviceProperty = Get-PnpDeviceProperty -InstanceId $InstanceId `
    -KeyName 'DEVPKEY_Device_Service' -ErrorAction SilentlyContinue
$service = if ($null -eq $serviceProperty) { '' } else { [string]$serviceProperty.Data }

if ($service -ne 'WinUSB') {
    throw "The device service is '$service', not 'WinUSB'. Bind the inbox WinUSB Device first."
}

$registryPath = 'Registry::HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\' +
    $InstanceId + '\Device Parameters'

if (-not (Test-Path -LiteralPath $registryPath)) {
    New-Item -Path $registryPath -Force | Out-Null
}

New-ItemProperty -LiteralPath $registryPath -Name 'DeviceInterfaceGUIDs' `
    -PropertyType MultiString -Value @($interfaceGuid) -Force | Out-Null

Write-Output "Registered $interfaceGuid for $($device.InstanceId)."
Write-Output 'Disconnect and reconnect the USB device on the same VM USB port.'
