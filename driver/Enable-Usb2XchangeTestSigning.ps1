# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Usb2Xchange-TestSupport.ps1')

Assert-Usb2XchangeAdministrator
$repoRoot = Get-Usb2XchangeRepoRoot
$signingState = Get-Usb2XchangeSigningStateRoot
$thumbprintPath = Join-Path $signingState 'thumbprint.txt'
$testPackage = Join-Path $repoRoot 'out\driver\test-package'
$testSys = Join-Path $testPackage 'usb2xchange-vminiport.sys'
$testCat = Join-Path $testPackage 'usb2xchange-vminiport.cat'

foreach ($path in @($thumbprintPath, $testSys, $testCat)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'Prepare and verify the test-signed package before changing boot policy.'
    }
}
if ((Get-AuthenticodeSignature -LiteralPath $testSys).Status -ne 'Valid' -or
    (Get-AuthenticodeSignature -LiteralPath $testCat).Status -ne 'Valid') {
    throw 'The test package signatures are not valid in the local trust stores.'
}

$secureBootState = Get-Usb2XchangeSecureBootState
if ($secureBootState -eq 'Enabled') {
    throw 'Secure Boot is enabled. This project will not disable it automatically.'
}
if ($secureBootState -like 'Unknown*') {
    throw "Secure Boot state is not known: $secureBootState"
}
if (@(Get-Usb2XchangeRootDevices).Count -ne 0) {
    throw 'A USB2Xchange root miniport already exists; refusing to change boot policy.'
}

$os = Get-CimInstance Win32_OperatingSystem
New-Item -ItemType Directory -Path $signingState -Force | Out-Null
$bootTimePath = Join-Path $signingState 'pre-reboot-boot-time.txt'
Set-Content -LiteralPath $bootTimePath `
    -Value $os.LastBootUpTime.ToFileTimeUtc() -Encoding Ascii
Sync-Usb2XchangeFile -Path $bootTimePath

& bcdedit.exe /set testsigning on
if ($LASTEXITCODE -ne 0) {
    throw 'BCDEdit could not enable TESTSIGNING. No driver was installed.'
}
$bootConfiguration = Get-Usb2XchangeBootConfiguration
if (-not (Test-Usb2XchangeTestSigningEnabled `
            -BootConfiguration $bootConfiguration)) {
    throw 'BCDEdit returned success but TESTSIGNING is not configured.'
}

Write-Output "Secure Boot: $secureBootState"
Write-Output 'TESTSIGNING is configured ON. No driver was installed.'
Write-Output 'Restart the VM, then rerun the audit before installation.'
