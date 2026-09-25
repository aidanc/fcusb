# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Usb2Xchange-TestSupport.ps1')

Assert-Usb2XchangeAdministrator
$tools = Get-Usb2XchangeToolPaths
$repoRoot = Get-Usb2XchangeRepoRoot
$unsignedPackage = Join-Path $repoRoot 'out\driver\package'
$testPackage = Join-Path $repoRoot 'out\driver\test-package'
$signingState = Get-Usb2XchangeSigningStateRoot
$sourceInf = Join-Path $unsignedPackage 'usb2xchange-vminiport.inf'
$sourceSys = Join-Path $unsignedPackage 'usb2xchange-vminiport.sys'

foreach ($source in @($sourceInf, $sourceSys)) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Build the unsigned package first; missing $source"
    }
}
if ((Get-AuthenticodeSignature -LiteralPath $sourceSys).Status -ne
        'NotSigned') {
    throw 'The source package is not the expected clean unsigned build.'
}

if (Test-Path -LiteralPath $testPackage) {
    Assert-Usb2XchangeChildPath -Candidate $testPackage `
        -Parent (Join-Path $repoRoot 'out')
    Remove-Item -LiteralPath $testPackage -Recurse -Force
}
New-Item -ItemType Directory -Path $testPackage -Force | Out-Null
New-Item -ItemType Directory -Path $signingState -Force | Out-Null
Copy-Item -LiteralPath $sourceInf,$sourceSys -Destination $testPackage

$personalCertificates = @(Get-Usb2XchangeTestCertificates -StoreName My)
if ($personalCertificates.Count -gt 1) {
    throw 'Multiple project test certificates exist in LocalMachine\My.'
}
if ($personalCertificates.Count -eq 0) {
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject $script:Usb2XchangeTestCertificateSubject `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).ToUniversalTime().AddYears(2)
} else {
    $certificate = $personalCertificates[0]
}
if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) {
    throw 'The project signing certificate lacks a usable private key.'
}
if (-not (Test-Usb2XchangeCodeSigningEku `
            -EnhancedKeyUsageList @($certificate.EnhancedKeyUsageList))) {
    throw 'The project signing certificate lacks the Code Signing EKU.'
}

$certificatePath = Join-Path $signingState 'usb2xchange-test.cer'
$thumbprintPath = Join-Path $signingState 'thumbprint.txt'
Export-Certificate -Cert $certificate -FilePath $certificatePath -Force |
    Out-Null
Set-Content -LiteralPath $thumbprintPath `
    -Value $certificate.Thumbprint -Encoding Ascii
Sync-Usb2XchangeFile -Path $certificatePath
Sync-Usb2XchangeFile -Path $thumbprintPath
foreach ($store in @('Root', 'TrustedPublisher')) {
    $match = @(Get-ChildItem -LiteralPath "Cert:\LocalMachine\$store" |
        Where-Object { $_.Thumbprint -eq $certificate.Thumbprint })
    if ($match.Count -eq 0) {
        Import-Certificate -FilePath $certificatePath `
            -CertStoreLocation "Cert:\LocalMachine\$store" | Out-Null
    }
}
$testSys = Join-Path $testPackage 'usb2xchange-vminiport.sys'
$testInf = Join-Path $testPackage 'usb2xchange-vminiport.inf'
$testCat = Join-Path $testPackage 'usb2xchange-vminiport.cat'

& $tools.SignTool sign /v /fd SHA256 /sm /s My `
    /sha1 $certificate.Thumbprint $testSys
if ($LASTEXITCODE -ne 0) {
    throw 'SignTool failed to embed the miniport test signature.'
}
Sync-Usb2XchangeFile -Path $testInf
Sync-Usb2XchangeFile -Path $testSys
& $tools.Inf2Cat "/driver:$testPackage" /os:10_X64
if ($LASTEXITCODE -ne 0) {
    throw 'Inf2Cat failed after the driver image was signed.'
}
& $tools.SignTool sign /v /fd SHA256 /sm /s My `
    /sha1 $certificate.Thumbprint $testCat
if ($LASTEXITCODE -ne 0) {
    throw 'SignTool failed to sign the driver package catalog.'
}
Sync-Usb2XchangeFile -Path $testCat
& $tools.SignTool verify /v /pa $testCat
if ($LASTEXITCODE -ne 0) {
    throw 'Catalog Authenticode verification failed.'
}
& $tools.SignTool verify /v /pa /c $testCat $testSys
if ($LASTEXITCODE -ne 0) {
    throw 'The signed driver image does not match the signed catalog.'
}

Write-Output "Test package: $testPackage"
Write-Output "Certificate: $($certificate.Subject)"
Write-Output "Thumbprint: $($certificate.Thumbprint)"
Write-Output "Expires: $($certificate.NotAfter.ToUniversalTime().ToString('o'))"
Write-Output "SYS status: $((Get-AuthenticodeSignature -LiteralPath $testSys).Status)"
Write-Output "CAT status: $((Get-AuthenticodeSignature -LiteralPath $testCat).Status)"
Write-Output 'TEST PACKAGE RESULT: signed and trusted locally; no boot or PnP state was changed.'
