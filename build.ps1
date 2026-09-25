# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $repoRoot 'scripts\AspiProviderBuildHash.ps1')
$processSearchPath = [Environment]::GetEnvironmentVariable('Path', 'Process')
[Environment]::SetEnvironmentVariable('PATH', $null, 'Process')
[Environment]::SetEnvironmentVariable('Path', $null, 'Process')
[Environment]::SetEnvironmentVariable('Path', $processSearchPath, 'Process')
$outputRoot = Join-Path $repoRoot 'out'
$binaryRoot = Join-Path $outputRoot 'bin'
$aspiRoot = Join-Path $outputRoot 'aspi'
$objectRoot = Join-Path $outputRoot 'obj'
$driverOutputRoot = Join-Path $outputRoot 'driver\Release'
$driverPackageRoot = Join-Path $outputRoot 'driver\package'
$csc64 = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$csc32 = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$ilasm32 = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\ilasm.exe'

foreach ($tool in @($csc64, $csc32, $ilasm32)) {
    if (-not (Test-Path -LiteralPath $tool)) {
        throw "A required .NET Framework build tool was not found at $tool"
    }
}

function Assert-ChildPath {
    param(
        [string]$Candidate,
        [string]$Parent
    )

    $candidatePath = [IO.Path]::GetFullPath($Candidate).TrimEnd('\')
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\')
    $parentPrefix = $parentPath + [IO.Path]::DirectorySeparatorChar
    if (-not $candidatePath.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing a recursive operation outside $parentPath`: $candidatePath"
    }
}

if ($Clean -and (Test-Path -LiteralPath $outputRoot)) {
    $cleanTargets = @(
        $binaryRoot,
        $aspiRoot,
        $objectRoot,
        $driverOutputRoot,
        $driverPackageRoot
    )
    foreach ($cleanTarget in $cleanTargets) {
        if (Test-Path -LiteralPath $cleanTarget) {
            Assert-ChildPath -Candidate $cleanTarget -Parent $outputRoot
            Remove-Item -LiteralPath $cleanTarget -Recurse -Force
        }
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

    $arguments = @(
        '/nologo',
        '/checked+',
        '/debug:pdbonly',
        '/optimize+',
        "/platform:$Platform",
        "/target:$Target",
        "/out:$Output"
    )
    foreach ($reference in $References) {
        $arguments += "/reference:$reference"
    }
    $arguments += $Sources

    & $Compiler $arguments
    if ($LASTEXITCODE -ne 0) {
        throw "C# compilation failed for $Output"
    }
}

$protocolDll = Join-Path $binaryRoot 'Usb2Xchange.Protocol.dll'
$winUsbDll = Join-Path $binaryRoot 'Usb2Xchange.WinUsb.dll'
$cliExe = Join-Path $binaryRoot 'usb2xchange.exe'
$testExe = Join-Path $binaryRoot 'Usb2Xchange.Tests.exe'
$flexColorPatchExe = Join-Path $binaryRoot 'flexcolor-aspi-patch.exe'
$flexColorPatchTestExe = Join-Path $binaryRoot `
    'Usb2Xchange.FlexColorPatch.Tests.exe'
$flexColorAspiLauncherExe = Join-Path $binaryRoot `
    'flexcolor-aspi-launcher.exe'
$managerExe = Join-Path $binaryRoot 'usb2xchange-manager.exe'
$aspiProtocolDll = Join-Path $aspiRoot 'Usb2Xchange.Protocol.dll'
$aspiWinUsbDll = Join-Path $aspiRoot 'Usb2Xchange.WinUsb.dll'
$aspiManagedDll = Join-Path $aspiRoot 'Usb2Xchange.AspiShim.Managed.dll'
$aspiNativeDll = Join-Path $aspiRoot 'wnaspi32.dll'
$aspiTestExe = Join-Path $aspiRoot 'Usb2Xchange.AspiShim.Tests.exe'
$aspiProbeExe = Join-Path $aspiRoot 'Usb2Xchange.AspiProbe.exe'
$scsiScanProbeExe = Join-Path $aspiRoot 'Usb2Xchange.ScsiScanProbe.exe'
$brokerProtocolDll = Join-Path $binaryRoot 'Usb2Xchange.BrokerProtocol.dll'
$brokerTestExe = Join-Path $binaryRoot 'Usb2Xchange.BrokerProtocol.Tests.exe'
$brokerExe = Join-Path $binaryRoot 'usb2xchange-broker.exe'
$brokerIntegrationTestExe = Join-Path $binaryRoot 'Usb2Xchange.Broker.Tests.exe'
$brokerNativeObject = Join-Path $binaryRoot 'broker-protocol-layout.obj'
$driverProject = Join-Path $repoRoot 'driver\Usb2Xchange.VMiniport\Usb2Xchange.VMiniport.vcxproj'
$driverSourceInf = Join-Path $repoRoot 'driver\Usb2Xchange.VMiniport\usb2xchange-vminiport.inf'
$driverBinary = Join-Path $driverOutputRoot 'usb2xchange-vminiport.sys'

$protocolSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.Protocol') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$winUsbSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.WinUsb') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$cliSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.Cli') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$testSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tests\Usb2Xchange.Tests') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$flexColorPatchSources = Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.FlexColorPatch') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$flexColorPatchTestSources = @(
    Join-Path $repoRoot `
        'src\Usb2Xchange.FlexColorPatch\FlexColorPatchEngine.cs'
    Join-Path $repoRoot `
        'tests\Usb2Xchange.FlexColorPatch.Tests\Program.cs'
)
$flexColorAspiLauncherSources = @(
    Join-Path $repoRoot `
        'src\Usb2Xchange.FlexColorPatch\FlexColorPatchEngine.cs'
)
$flexColorAspiLauncherSources += Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.FlexColorAspiLauncher') `
    -Filter '*.cs' | Select-Object -ExpandProperty FullName
$managerSources = Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.Manager') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$aspiSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.AspiShim') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$aspiTestSources = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tests\Usb2Xchange.AspiShim.Tests') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName)
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\PreviewObserveLogInspector.cs'
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\OfflineFullScanLogInspector.cs'
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\OfflineOperatorRepeatLogInspector.cs'
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\LiveOperatorRepeatLogInspector.cs'
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\TiffOutputInspector.cs'
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\FullScanStartupFingerprintLogInspector.cs'
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\FullScanCompletionLogInspector.cs'
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\FullScanTerminalProbeLogInspector.cs'
$aspiTestSources += Join-Path $repoRoot `
    'src\Usb2Xchange.FlexColorAspiLauncher\FlexColorProgressMonitor.cs'
$aspiProbeSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.AspiProbe') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$scsiScanProbeSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.ScsiScanProbe') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$brokerProtocolSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.BrokerProtocol') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$brokerTestSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tests\Usb2Xchange.BrokerProtocol.Tests') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$brokerSources = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.Broker') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
$brokerIntegrationTestSources = @(
    Join-Path $repoRoot 'src\Usb2Xchange.Broker\StoragePortDiscovery.cs'
    Join-Path $repoRoot 'src\Usb2Xchange.Broker\ReadOnlyScsiExecutor.cs'
    Join-Path $repoRoot 'tests\Usb2Xchange.Broker.Tests\Program.cs'
)

Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'library' -Output $protocolDll -Sources $protocolSources
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'library' -Output $winUsbDll -Sources $winUsbSources -References @($protocolDll)
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' -Output $cliExe -Sources $cliSources -References @($protocolDll, $winUsbDll)
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' -Output $testExe -Sources $testSources -References @($protocolDll)
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' -Output $flexColorPatchExe -Sources $flexColorPatchSources
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' -Output $flexColorPatchTestExe -Sources $flexColorPatchTestSources
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'library' -Output $brokerProtocolDll -Sources $brokerProtocolSources
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' -Output $brokerTestExe -Sources $brokerTestSources -References @($brokerProtocolDll)
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' -Output $brokerExe -Sources $brokerSources -References @($protocolDll, $winUsbDll, $brokerProtocolDll)
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' -Output $brokerIntegrationTestExe -Sources $brokerIntegrationTestSources -References @($protocolDll, $brokerProtocolDll)

Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'library' -Output $aspiProtocolDll -Sources $protocolSources
Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'library' -Output $aspiWinUsbDll -Sources $winUsbSources -References @($aspiProtocolDll)
Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'exe' -Output $scsiScanProbeExe -Sources $scsiScanProbeSources -References @($aspiProtocolDll)
Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'library' -Output $aspiManagedDll -Sources $aspiSources -References @($aspiProtocolDll, $aspiWinUsbDll)

$entryPoints = Join-Path $repoRoot 'src\Usb2Xchange.AspiShim\NativeEntryPoints.il'
& $ilasm32 /nologo /quiet /dll "/output=$aspiNativeDll" $entryPoints
if ($LASTEXITCODE -ne 0) {
    throw "IL assembly failed for $aspiNativeDll"
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.AspiShim\wnaspi32.dll.config') -Destination $aspiRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\Usb2Xchange.AspiShim\FlexColor.exe.config') -Destination $aspiRoot -Force
$providerHashSource = Join-Path $aspiRoot `
    'AspiProviderBuildHashes.g.cs'
$flexColorAspiLauncherSources += New-AspiProviderHashSource `
    -Destination $providerHashSource -NativeProvider $aspiNativeDll `
    -ManagedShim $aspiManagedDll -WinUsb $aspiWinUsbDll `
    -Protocol $aspiProtocolDll `
    -ProviderConfig (Join-Path $aspiRoot 'wnaspi32.dll.config') `
    -FlexColorConfig (Join-Path $aspiRoot 'FlexColor.exe.config')
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'exe' `
    -Output $flexColorAspiLauncherExe `
    -Sources $flexColorAspiLauncherSources
Invoke-Csc -Compiler $csc64 -Platform 'x64' -Target 'winexe' `
    -Output $managerExe -Sources $managerSources `
    -References @('System.Windows.Forms.dll', 'System.Drawing.dll')

Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'exe' -Output $aspiTestExe -Sources $aspiTestSources -References @($aspiProtocolDll, $aspiWinUsbDll, $aspiManagedDll)
Invoke-Csc -Compiler $csc32 -Platform 'x86' -Target 'exe' -Output $aspiProbeExe -Sources $aspiProbeSources -References @($aspiProtocolDll, $aspiManagedDll)

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw "Visual Studio Build Tools discovery utility was not found at $vswhere"
}
$vsInstall = & $vswhere -latest -products Microsoft.VisualStudio.Product.BuildTools -requires Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64 -property installationPath
if (-not $vsInstall) {
    throw 'Visual Studio 2022 MSVC 14.44 x64 build tools are required.'
}
$msvcRoot = Join-Path $vsInstall 'VC\Tools\MSVC'
$msvcVersion = Get-Item -LiteralPath (Join-Path $msvcRoot '14.44.35207') -ErrorAction SilentlyContinue
if (-not $msvcVersion) {
    throw 'Pinned MSVC tool directory 14.44.35207 is required.'
}
$cl = Join-Path $msvcVersion.FullName 'bin\Hostx64\x64\cl.exe'
if (-not (Test-Path -LiteralPath $cl)) {
    throw "The x64 C compiler was not found at $cl"
}
$msbuild = Join-Path $vsInstall 'MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuild)) {
    throw "MSBuild was not found at $msbuild"
}
$inf2Cat = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\10.0.26100.0\x86\Inf2Cat.exe'
if (-not (Test-Path -LiteralPath $inf2Cat)) {
    throw "Inf2Cat was not found at $inf2Cat"
}
$brokerNativeSource = Join-Path $repoRoot 'tests\Usb2Xchange.BrokerProtocol.NativeTests\layout_test.c'
& $cl /nologo /W4 /WX /c "/Fo$brokerNativeObject" $brokerNativeSource
if ($LASTEXITCODE -ne 0) {
    throw 'Native broker ABI layout compilation failed.'
}

& $msbuild $driverProject /m /t:Rebuild /p:Configuration=Release /p:Platform=x64 /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw 'The AMD64 Storport virtual miniport build failed.'
}

if (Test-Path -LiteralPath $driverPackageRoot) {
    Assert-ChildPath -Candidate $driverPackageRoot -Parent $outputRoot
    Remove-Item -LiteralPath $driverPackageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $driverPackageRoot -Force | Out-Null
Copy-Item -LiteralPath $driverBinary -Destination $driverPackageRoot
Copy-Item -LiteralPath $driverSourceInf -Destination $driverPackageRoot
& $inf2Cat "/driver:$driverPackageRoot" '/os:10_X64'
if ($LASTEXITCODE -ne 0) {
    throw 'Inf2Cat validation of the unsigned AMD64 driver package failed.'
}

Write-Output "Built $cliExe"
Write-Output "Built $testExe"
Write-Output "Built $flexColorPatchExe"
Write-Output "Built $flexColorPatchTestExe"
Write-Output "Built $flexColorAspiLauncherExe"
Write-Output "Built $aspiNativeDll"
Write-Output "Built $aspiTestExe"
Write-Output "Built $aspiProbeExe"
Write-Output "Built $scsiScanProbeExe"
Write-Output "Built $brokerProtocolDll"
Write-Output "Built $brokerTestExe"
Write-Output "Built $brokerExe"
Write-Output "Built $brokerIntegrationTestExe"
Write-Output "Verified native broker ABI layout in $brokerNativeObject"
Write-Output "Built unsigned Storport miniport $driverBinary"
Write-Output "Validated unsigned driver package in $driverPackageRoot"
