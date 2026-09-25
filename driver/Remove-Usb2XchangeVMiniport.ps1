# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
[CmdletBinding()]
param(
    [switch]$RemoveTestCertificate,
    [switch]$DisableTestSigning
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Usb2Xchange-TestSupport.ps1')

Assert-Usb2XchangeAdministrator
$tools = Get-Usb2XchangeToolPaths
$devices = @(Get-Usb2XchangeRootDevices)
if ($devices.Count -gt 1) {
    throw 'Multiple USB2Xchange root miniports exist; refusing broad removal.'
}

$publishedInf = $null
if ($devices.Count -eq 1) {
    $device = $devices[0]
    $infProperty = Get-PnpDeviceProperty -InstanceId $device.InstanceId `
        -KeyName 'DEVPKEY_Device_DriverInfPath' -ErrorAction Stop
    $publishedInf = [string]$infProperty.Data
    if ($publishedInf -notmatch '^oem[0-9]+\.inf$') {
        throw "Unexpected published INF name: $publishedInf"
    }
    & $tools.DevCon remove "@$($device.InstanceId)"
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1) {
        throw "DevCon removal failed with exit code $LASTEXITCODE."
    }
    & pnputil.exe /delete-driver $publishedInf /uninstall /force
    $pnpUtilExit = $LASTEXITCODE
    if ($pnpUtilExit -ne 0 -and $pnpUtilExit -ne 3010) {
        throw "PnPUtil could not delete $publishedInf (exit $pnpUtilExit)."
    }
    Write-Output "Removed root device $($device.InstanceId) and $publishedInf."
    if ($pnpUtilExit -eq 3010) {
        Write-Output 'PnPUtil requested a restart to finish package removal.'
    }
} else {
    Write-Output 'No USB2Xchange root miniport is installed.'
}

$serviceRegistryPath =
    "Registry::HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\$script:Usb2XchangeMiniportService"
if (Test-Path -LiteralPath $serviceRegistryPath) {
    $serviceProperties = Get-ItemProperty -LiteralPath $serviceRegistryPath
    $expectedImagePath =
        '\SystemRoot\System32\drivers\usb2xchange-vminiport.sys'
    if ([int]$serviceProperties.Type -ne 1 -or
        -not [string]::Equals([string]$serviceProperties.ImagePath,
            $expectedImagePath,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The residual service does not have the expected kernel-driver identity.'
    }

    $serviceEnumPath = Join-Path $serviceRegistryPath 'Enum'
    if (Test-Path -LiteralPath $serviceEnumPath) {
        $serviceEnum = Get-ItemProperty -LiteralPath $serviceEnumPath
        if ([int]$serviceEnum.Count -ne 0) {
            throw 'The residual service still owns device instances; refusing deletion.'
        }
    }

    $serviceQuery = @(& sc.exe query $script:Usb2XchangeMiniportService 2>&1)
    if ($LASTEXITCODE -ne 0 -or
        -not ($serviceQuery -match '^\s*STATE\s+:\s+1\s+STOPPED\s*$')) {
        throw 'The residual service is not verifiably stopped; refusing deletion.'
    }

    & sc.exe delete $script:Usb2XchangeMiniportService
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not delete the stopped residual USB2Xchange service.'
    }
    Write-Output "Removed stopped residual service $script:Usb2XchangeMiniportService."
}

if ($RemoveTestCertificate) {
    $thumbprintPath = Join-Path (Get-Usb2XchangeSigningStateRoot) `
        'thumbprint.txt'
    if (-not (Test-Path -LiteralPath $thumbprintPath -PathType Leaf)) {
        throw 'Certificate marker is missing; refusing subject-wide deletion.'
    }
    $thumbprint = (Get-Content -LiteralPath $thumbprintPath -Raw).Trim()
    foreach ($store in @('My', 'Root', 'TrustedPublisher')) {
        $path = "Cert:\LocalMachine\$store\$thumbprint"
        if (Test-Path -LiteralPath $path) {
            $certificate = Get-Item -LiteralPath $path
            if ($certificate.Subject -ne
                    $script:Usb2XchangeTestCertificateSubject) {
                throw "Certificate $thumbprint has an unexpected subject."
            }
            Remove-Item -LiteralPath $path -Force
            Write-Output "Removed certificate $thumbprint from $store."
        }
    }
}

if ($DisableTestSigning) {
    & bcdedit.exe /set testsigning off
    if ($LASTEXITCODE -ne 0) {
        throw 'BCDEdit could not disable TESTSIGNING.'
    }
    Write-Output 'TESTSIGNING is configured OFF. Restart the VM to apply it.'
}
