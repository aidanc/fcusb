# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
Set-StrictMode -Version Latest

$script:Usb2XchangeTestCertificateSubject =
    'CN=USB2Xchange Development Test Certificate'
$script:Usb2XchangeRootHardwareId = 'ROOT\USB2XCHANGEVMINIPORT'
$script:Usb2XchangeRootInstancePrefix = 'ROOT\SCSIADAPTER\'
$script:Usb2XchangeMiniportService = 'usb2xchange-vminiport'

function Assert-Usb2XchangeAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from a PowerShell window opened with Run as administrator.'
    }
}

function Get-Usb2XchangeRepoRoot {
    return Split-Path -Parent $PSScriptRoot
}

function Assert-Usb2XchangeChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Candidate,
        [Parameter(Mandatory = $true)]
        [string]$Parent
    )

    $candidatePath = [IO.Path]::GetFullPath($Candidate).TrimEnd('\')
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\')
    $parentPrefix = $parentPath + [IO.Path]::DirectorySeparatorChar
    if (-not $candidatePath.StartsWith(
            $parentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing an operation outside $parentPath`: $candidatePath"
    }
}

function Sync-Usb2XchangeFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $repoRoot = Get-Usb2XchangeRepoRoot
    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-Usb2XchangeChildPath -Candidate $fullPath `
        -Parent (Join-Path $repoRoot 'out')
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Cannot flush missing file $fullPath"
    }

    $stream = [IO.File]::Open(
        $fullPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::Read)
    try {
        $stream.Flush($true)
    } finally {
        $stream.Dispose()
    }
}

function Get-Usb2XchangeToolPaths {
    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
    $tools = [ordered]@{
        Inf2Cat = Join-Path $kitsRoot 'bin\10.0.26100.0\x86\Inf2Cat.exe'
        SignTool = Join-Path $kitsRoot 'bin\10.0.26100.0\x64\signtool.exe'
        DevCon = Join-Path $kitsRoot 'Tools\10.0.26100.0\x64\devcon.exe'
    }
    foreach ($entry in $tools.GetEnumerator()) {
        if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
            throw "Required WDK tool $($entry.Key) was not found at $($entry.Value)"
        }
    }
    return [pscustomobject]$tools
}

function Get-Usb2XchangeBootConfiguration {
    $output = & bcdedit.exe /enum 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "BCDEdit audit failed: $($output -join ' ')"
    }
    return @($output)
}

function Test-Usb2XchangeTestSigningEnabled {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string[]]$BootConfiguration
    )

    return [bool]($BootConfiguration -match '^\s*testsigning\s+Yes\s*$')
}

function Test-Usb2XchangeCodeSigningEku {
    param(
        [AllowEmptyCollection()]
        [object[]]$EnhancedKeyUsageList
    )

    $matches = @($EnhancedKeyUsageList | Where-Object {
            $objectId = $_.ObjectId
            $objectIdValue = if ($objectId -is [Security.Cryptography.Oid]) {
                $objectId.Value
            } else {
                [string]$objectId
            }
            $objectIdValue -eq '1.3.6.1.5.5.7.3.3'
        })
    return $matches.Count -eq 1
}

function Get-Usb2XchangeSecureBootState {
    try {
        if (Confirm-SecureBootUEFI -ErrorAction Stop) {
            return 'Enabled'
        }
        return 'Disabled'
    } catch [PlatformNotSupportedException] {
        return 'UnsupportedOrLegacyBIOS'
    } catch {
        $registryPath =
            'Registry::HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\State'
        $registryValue = Get-ItemProperty -LiteralPath $registryPath `
            -Name UEFISecureBootEnabled -ErrorAction SilentlyContinue
        if ($null -ne $registryValue) {
            return $(if ($registryValue.UEFISecureBootEnabled -eq 1) {
                    'Enabled'
                } else {
                    'Disabled'
                })
        }
        return "Unknown ($($_.Exception.Message))"
    }
}

function Get-Usb2XchangeHvciState {
    $path =
        'Registry::HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity'
    $value = Get-ItemProperty -LiteralPath $path -Name Enabled `
        -ErrorAction SilentlyContinue
    if ($null -eq $value) {
        return 'NotConfigured'
    }
    return $(if ($value.Enabled -eq 1) { 'Enabled' } else { 'Disabled' })
}

function Get-Usb2XchangeRootDevices {
    $matches = @()
    $candidates = @(Get-PnpDevice -Class SCSIAdapter -ErrorAction Stop |
        Where-Object {
            $_.InstanceId.StartsWith(
                $script:Usb2XchangeRootInstancePrefix,
                [StringComparison]::OrdinalIgnoreCase)
        })
    foreach ($candidate in $candidates) {
        try {
            $service = Get-PnpDeviceProperty -InstanceId $candidate.InstanceId `
                -KeyName 'DEVPKEY_Device_Service' -ErrorAction Stop
            $hardwareIds = Get-PnpDeviceProperty `
                -InstanceId $candidate.InstanceId `
                -KeyName 'DEVPKEY_Device_HardwareIds' -ErrorAction Stop
        } catch {
            continue
        }
        $hasHardwareId = @($hardwareIds.Data | Where-Object {
                [string]::Equals([string]$_,
                    $script:Usb2XchangeRootHardwareId,
                    [StringComparison]::OrdinalIgnoreCase)
            }).Count -ne 0
        if ([string]::Equals([string]$service.Data,
                $script:Usb2XchangeMiniportService,
                [StringComparison]::OrdinalIgnoreCase) -and
            $hasHardwareId) {
            $matches += $candidate
        }
    }
    return $matches
}

function Get-Usb2XchangeTestCertificates {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StoreName
    )

    return @(Get-ChildItem -LiteralPath "Cert:\LocalMachine\$StoreName" |
        Where-Object {
            $_.Subject -eq $script:Usb2XchangeTestCertificateSubject
        })
}

function Get-Usb2XchangeSigningStateRoot {
    $root = Join-Path (Get-Usb2XchangeRepoRoot) 'out\driver\test-signing'
    Assert-Usb2XchangeChildPath -Candidate $root `
        -Parent (Join-Path (Get-Usb2XchangeRepoRoot) 'out')
    return $root
}
