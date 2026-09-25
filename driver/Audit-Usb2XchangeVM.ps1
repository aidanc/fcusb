# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
[CmdletBinding()]
param(
    [string]$ReportPath,
    [switch]$PostSigning
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Usb2Xchange-TestSupport.ps1')

Assert-Usb2XchangeAdministrator
$repoRoot = Get-Usb2XchangeRepoRoot
$tools = Get-Usb2XchangeToolPaths
$bootConfiguration = Get-Usb2XchangeBootConfiguration
$os = Get-CimInstance Win32_OperatingSystem
$computer = Get-CimInstance Win32_ComputerSystem
$rootDevices = @(Get-Usb2XchangeRootDevices)
$usbDevices = @(Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object {
        $_.InstanceId -like 'USB\VID_03F3&PID_2002\*' -or
        $_.InstanceId -like 'USB\VID_03F3&PID_2003\*'
    })
$certificates = @()
foreach ($store in @('My', 'Root', 'TrustedPublisher')) {
    foreach ($certificate in Get-Usb2XchangeTestCertificates -StoreName $store) {
        $certificates += [pscustomobject]@{
            Store = $store
            Thumbprint = $certificate.Thumbprint
            HasPrivateKey = $certificate.HasPrivateKey
            NotAfter = $certificate.NotAfter
        }
    }
}
$expectedThumbprint = $null
$previousBootFileTime = $null
$packageSignatures = @()
if ($PostSigning) {
    $signingState = Get-Usb2XchangeSigningStateRoot
    $thumbprintPath = Join-Path $signingState 'thumbprint.txt'
    $bootTimePath = Join-Path $signingState 'pre-reboot-boot-time.txt'
    $testPackage = Join-Path $repoRoot 'out\driver\test-package'
    $signatureFiles = @(
        [pscustomobject]@{
            Role = 'SYS'
            Path = Join-Path $testPackage 'usb2xchange-vminiport.sys'
        },
        [pscustomobject]@{
            Role = 'CAT'
            Path = Join-Path $testPackage 'usb2xchange-vminiport.cat'
        }
    )
    foreach ($path in @($thumbprintPath, $bootTimePath) +
        @($signatureFiles.Path)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Post-signing audit is missing required state: $path"
        }
    }
    $expectedThumbprint = (Get-Content -LiteralPath $thumbprintPath -Raw).Trim()
    if ($expectedThumbprint -notmatch '^[0-9A-Fa-f]{40}$') {
        throw 'Post-signing audit found an invalid certificate thumbprint marker.'
    }
    $previousBootText = (Get-Content -LiteralPath $bootTimePath -Raw).Trim()
    $previousBootFileTime = 0L
    if (-not [long]::TryParse($previousBootText, [ref]$previousBootFileTime)) {
        throw 'Post-signing audit found an invalid pre-reboot boot-time marker.'
    }
    foreach ($signatureFile in $signatureFiles) {
        $signature = Get-AuthenticodeSignature `
            -LiteralPath $signatureFile.Path
        $signatureThumbprint = if ($null -ne $signature.SignerCertificate) {
            $signature.SignerCertificate.Thumbprint
        } else {
            $null
        }
        $nonzeroBytes = @([IO.File]::ReadAllBytes($signatureFile.Path) |
            Where-Object { $_ -ne 0 }).Count
        $packageSignatures += [pscustomobject]@{
            Role = $signatureFile.Role
            Path = $signatureFile.Path
            Status = [string]$signature.Status
            Thumbprint = $signatureThumbprint
            NonzeroBytes = $nonzeroBytes
        }
    }
}

$report = [ordered]@{
    Mode = $(if ($PostSigning) { 'PostSigning' } else { 'Prerequisite' })
    Computer = $computer.Name
    OsCaption = $os.Caption
    OsVersion = $os.Version
    OsArchitecture = $os.OSArchitecture
    LastBootUtc = $os.LastBootUpTime.ToUniversalTime().ToString('o')
    LastBootFileTimeUtc = $os.LastBootUpTime.ToFileTimeUtc()
    SecureBoot = Get-Usb2XchangeSecureBootState
    Hvci = Get-Usb2XchangeHvciState
    TestSigningConfigured =
        Test-Usb2XchangeTestSigningEnabled `
            -BootConfiguration $bootConfiguration
    RootMiniportDeviceCount = $rootDevices.Count
    UsbDevices = @($usbDevices | ForEach-Object {
            [ordered]@{
                Status = $_.Status
                FriendlyName = $_.FriendlyName
                InstanceId = $_.InstanceId
            }
        })
    ProjectCertificates = $certificates
    ExpectedThumbprint = $expectedThumbprint
    PreviousBootFileTimeUtc = $previousBootFileTime
    PackageSignatures = $packageSignatures
    Tools = [ordered]@{
        Inf2Cat = $tools.Inf2Cat
        SignTool = $tools.SignTool
        DevCon = $tools.DevCon
    }
}
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    Assert-Usb2XchangeChildPath -Candidate $ReportPath `
        -Parent (Join-Path $repoRoot 'out')
    $reportDirectory = Split-Path -Parent $ReportPath
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $report | ConvertTo-Json -Depth 5 | Set-Content `
        -LiteralPath $ReportPath -Encoding UTF8
}

Write-Output "Computer: $($report.Computer)"
Write-Output "Audit mode: $($report.Mode)"
Write-Output "OS: $($report.OsCaption) $($report.OsVersion) $($report.OsArchitecture)"
Write-Output "Last boot: $($report.LastBootUtc)"
Write-Output "Secure Boot: $($report.SecureBoot)"
Write-Output "Memory Integrity/HVCI: $($report.Hvci)"
Write-Output "TESTSIGNING configured: $($report.TestSigningConfigured)"
Write-Output "Root miniport devices: $($rootDevices.Count)"
Write-Output "USB2Xchange USB devices: $($usbDevices.Count)"
foreach ($device in $usbDevices) {
    Write-Output "  $($device.Status) | $($device.FriendlyName) | $($device.InstanceId)"
}
Write-Output "Project test certificates: $($certificates.Count)"
foreach ($certificate in $certificates) {
    Write-Output "  $($certificate.Store) | $($certificate.Thumbprint) | private=$($certificate.HasPrivateKey) | expires=$($certificate.NotAfter.ToString('o'))"
}
foreach ($signature in $packageSignatures) {
    Write-Output "  $($signature.Role) | $($signature.Status) | $($signature.Thumbprint) | nonzero=$($signature.NonzeroBytes)"
}
Write-Output "Inf2Cat: $($tools.Inf2Cat)"
Write-Output "SignTool: $($tools.SignTool)"
Write-Output "DevCon: $($tools.DevCon)"

if ($rootDevices.Count -ne 0) {
    throw 'Audit refused: a USB2Xchange root miniport already exists.'
}
if ($usbDevices.Count -ne 1 -or
    $usbDevices[0].InstanceId -notlike 'USB\VID_03F3&PID_2003\*' -or
    $usbDevices[0].Status -ne 'OK') {
    throw 'Audit refused: expected exactly one operational PID-2003 USB2Xchange device.'
}

if ($PostSigning) {
    if (-not $report.TestSigningConfigured) {
        throw 'Post-signing audit refused: TESTSIGNING is not configured.'
    }
    if ($report.LastBootFileTimeUtc -eq $previousBootFileTime) {
        throw 'Post-signing audit refused: the required reboot has not occurred.'
    }
    if ($report.SecureBoot -eq 'Enabled' -or
        $report.SecureBoot -like 'Unknown*') {
        throw "Post-signing audit refused: Secure Boot state is $($report.SecureBoot)."
    }
    if ($report.Hvci -eq 'Enabled') {
        throw 'Post-signing audit refused: Memory Integrity/HVCI is enabled.'
    }
    if ($certificates.Count -ne 3) {
        throw 'Post-signing audit refused: expected one project certificate in each required store.'
    }
    foreach ($store in @('My', 'Root', 'TrustedPublisher')) {
        $storeMatches = @($certificates | Where-Object { $_.Store -eq $store })
        if ($storeMatches.Count -ne 1 -or
            $storeMatches[0].Thumbprint -ne $expectedThumbprint) {
            throw "Post-signing audit refused: certificate mismatch in LocalMachine\$store."
        }
    }
    $personalCertificate = @($certificates |
        Where-Object { $_.Store -eq 'My' })[0]
    if (-not $personalCertificate.HasPrivateKey) {
        throw 'Post-signing audit refused: signing certificate private key is missing.'
    }
    if ($packageSignatures.Count -ne 2 -or
        @($packageSignatures | Where-Object {
                $_.Status -ne 'Valid' -or
                $_.Thumbprint -ne $expectedThumbprint -or
                $_.NonzeroBytes -eq 0
            }).Count -ne 0) {
        throw 'Post-signing audit refused: signed package validation failed.'
    }
    Write-Output 'AUDIT RESULT: post-reboot signing state is valid; no state was changed.'
} else {
    if ($certificates.Count -ne 0) {
        throw 'Audit refused: a USB2Xchange test certificate already exists.'
    }
    Write-Output 'AUDIT RESULT: prerequisites are clean; no state was changed.'
}
