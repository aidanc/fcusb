# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
. (Join-Path $repoRoot 'scripts\AspiProviderBuildHash.ps1')
$binaryRoot = Join-Path $repoRoot 'out\bin'
$aspiRoot = Join-Path $repoRoot 'out\aspi'
$csc64 = Join-Path $env:WINDIR `
    'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$csc32 = Join-Path $env:WINDIR `
    'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$ilasm32 = Join-Path $env:WINDIR `
    'Microsoft.NET\Framework\v4.0.30319\ilasm.exe'
foreach ($tool in @($csc64, $csc32, $ilasm32)) {
    if (-not (Test-Path -LiteralPath $tool)) {
        throw "Required .NET Framework tool was not found: $tool"
    }
}
New-Item -ItemType Directory -Path $binaryRoot -Force | Out-Null
New-Item -ItemType Directory -Path $aspiRoot -Force | Out-Null

function Invoke-Csc {
    param(
        [string]$Compiler,
        [string]$Platform,
        [string]$Target,
        [string]$Output,
        [string[]]$Sources,
        [string[]]$References = @()
    )
    $arguments = @('/nologo', '/checked+', '/debug:pdbonly', '/optimize+',
        "/platform:$Platform", "/target:$Target", "/out:$Output")
    foreach ($reference in $References) {
        $arguments += "/reference:$reference"
    }
    $arguments += $Sources
    & $Compiler $arguments
    if ($LASTEXITCODE -ne 0) {
        throw "C# compilation failed for $Output"
    }
}

& (Join-Path $repoRoot 'scripts\Build-FlexColorAspiPatch.ps1')

$protocolDll = Join-Path $aspiRoot 'Usb2Xchange.Protocol.dll'
$winUsbDll = Join-Path $aspiRoot 'Usb2Xchange.WinUsb.dll'
$managedDll = Join-Path $aspiRoot 'Usb2Xchange.AspiShim.Managed.dll'
$nativeDll = Join-Path $aspiRoot 'wnaspi32.dll'
$testExe = Join-Path $aspiRoot 'Usb2Xchange.AspiShim.Tests.exe'
$probeExe = Join-Path $aspiRoot 'Usb2Xchange.AspiProbe.exe'
$launcherExe = Join-Path $binaryRoot 'flexcolor-aspi-launcher.exe'
$managerExe = Join-Path $binaryRoot 'usb2xchange-manager.exe'

$protocolSources = Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.Protocol') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$winUsbSources = Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.WinUsb') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$aspiSources = Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.AspiShim') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$testSources = @(Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'tests\Usb2Xchange.AspiShim.Tests') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName)
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\PreviewObserveLogInspector.cs'
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\OfflineFullScanLogInspector.cs'
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\OfflineOperatorRepeatLogInspector.cs'
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\LiveOperatorRepeatLogInspector.cs'
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\TiffOutputInspector.cs'
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\FullScanStartupFingerprintLogInspector.cs'
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\FullScanCompletionLogInspector.cs'
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\FullScanTerminalProbeLogInspector.cs'
$testSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\FlexColorProgressMonitor.cs'
$probeSources = Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.AspiProbe') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$launcherSources = @(
    Join-Path $repoRoot `
        'src\Usb2Xchange.FlexColorPatch\FlexColorPatchEngine.cs'
)
$launcherSources += Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.FlexColorAspiLauncher') `
    -Filter '*.cs' | Select-Object -ExpandProperty FullName
$managerSources = Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.Manager') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName

Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'library' `
    -Output $protocolDll -Sources $protocolSources
Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'library' `
    -Output $winUsbDll -Sources $winUsbSources -References @($protocolDll)
Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'library' `
    -Output $managedDll -Sources $aspiSources `
    -References @($protocolDll, $winUsbDll)

$entryPoints = Join-Path $repoRoot `
    'src\Usb2Xchange.AspiShim\NativeEntryPoints.il'
& $ilasm32 /nologo /quiet /dll "/output=$nativeDll" $entryPoints
if ($LASTEXITCODE -ne 0) {
    throw "IL assembly failed for $nativeDll"
}

Copy-Item -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.AspiShim\wnaspi32.dll.config') `
    -Destination $aspiRoot -Force
Copy-Item -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.AspiShim\FlexColor.exe.config') `
    -Destination $aspiRoot -Force
$providerHashSource = Join-Path $aspiRoot `
    'AspiProviderBuildHashes.g.cs'
$launcherSources += New-AspiProviderHashSource `
    -Destination $providerHashSource -NativeProvider $nativeDll `
    -ManagedShim $managedDll -WinUsb $winUsbDll -Protocol $protocolDll `
    -ProviderConfig (Join-Path $aspiRoot 'wnaspi32.dll.config') `
    -FlexColorConfig (Join-Path $aspiRoot 'FlexColor.exe.config')

Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'exe' `
    -Output $testExe -Sources $testSources `
    -References @($protocolDll, $winUsbDll, $managedDll)
Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'exe' `
    -Output $probeExe -Sources $probeSources `
    -References @($protocolDll, $managedDll)
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' `
    -Output $launcherExe -Sources $launcherSources
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'winexe' `
    -Output $managerExe -Sources $managerSources `
    -References @('System.Windows.Forms.dll', 'System.Drawing.dll')

Write-Output "Built $nativeDll"
Write-Output "Built $testExe"
Write-Output "Built $probeExe"
Write-Output "Built $launcherExe"
Write-Output "Built $managerExe"
