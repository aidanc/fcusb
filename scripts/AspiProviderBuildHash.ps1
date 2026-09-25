# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
function New-AspiProviderHashSource {
    param(
        [string]$Destination,
        [string]$NativeProvider,
        [string]$ManagedShim,
        [string]$WinUsb,
        [string]$Protocol,
        [string]$ProviderConfig,
        [string]$FlexColorConfig
    )
    $nativeHash = (Get-FileHash -Algorithm SHA256 `
        -LiteralPath $NativeProvider).Hash
    $managedHash = (Get-FileHash -Algorithm SHA256 `
        -LiteralPath $ManagedShim).Hash
    $winUsbHash = (Get-FileHash -Algorithm SHA256 `
        -LiteralPath $WinUsb).Hash
    $protocolHash = (Get-FileHash -Algorithm SHA256 `
        -LiteralPath $Protocol).Hash
    $providerConfigHash = (Get-FileHash -Algorithm SHA256 `
        -LiteralPath $ProviderConfig).Hash
    $flexColorConfigHash = (Get-FileHash -Algorithm SHA256 `
        -LiteralPath $FlexColorConfig).Hash
    $source = @(
        'namespace Usb2Xchange.FlexColorAspiLauncher',
        '{',
        '    internal static class AspiProviderBuildHashes',
        '    {',
        '        internal const string NativeProviderSha256 =',
        "            `"$nativeHash`";",
        '        internal const string ManagedShimSha256 =',
        "            `"$managedHash`";",
        '        internal const string WinUsbSha256 =',
        "            `"$winUsbHash`";",
        '        internal const string ProtocolSha256 =',
        "            `"$protocolHash`";",
        '        internal const string ProviderConfigSha256 =',
        "            `"$providerConfigHash`";",
        '        internal const string FlexColorConfigSha256 =',
        "            `"$flexColorConfigHash`";",
        '    }',
        '}'
    )
    Set-Content -LiteralPath $Destination -Value $source -Encoding UTF8
    return $Destination
}
