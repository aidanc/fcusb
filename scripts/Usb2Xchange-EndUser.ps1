# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [ValidateSet(
        'AcknowledgeRisk',
        'Status',
        'Install',
        'Repair',
        'Start',
        'Stop',
        'DeviceStatus',
        'RegisterPresentInterface',
        'InitializeAdapter',
        'OpenLogs',
        'Uninstall')]
    [string]$Action = 'Status',

    [string]$SourceRoot,

    [string]$AdapterFirmwarePath,

    [string]$LegacyRuntimeRoot,

    [string]$UserRoot,

    [switch]$NoDesktopShortcut,

    [switch]$NoShellIntegration,

    [switch]$Json,

    [int]$WaitForProcessId
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packageRoot = [IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))).
    TrimEnd('\')
if ([string]::IsNullOrWhiteSpace($UserRoot)) {
    $UserRoot = Join-Path $env:LOCALAPPDATA 'USB2Xchange'
}
$user = [IO.Path]::GetFullPath($UserRoot).TrimEnd('\')
$install = Join-Path $user 'App'
$configurationPath = Join-Path $user 'configuration.json'
$firmwareDirectory = Join-Path $user 'UserData'
$storedFirmwarePath = Join-Path $firmwareDirectory 'usb2xchange.fw'
$expectedFirmwareHash =
    'D0967EF81E71E9293D0499C91D687E2409F8CA13B14FFE2C4F35F07685D25FBD'
$interfaceGuid = '{86A64B6A-BC77-49D2-B378-0F43E5DAA568}'
$requiredPackagePaths = @(
    'USB2Xchange.exe',
    'out/bin/usb2xchange.exe',
    'out/bin/Usb2Xchange.WinUsb.dll',
    'out/bin/Usb2Xchange.Protocol.dll',
    'scripts/Usb2Xchange-EndUser.ps1',
    'scripts/Usb2Xchange-FlexColor.ps1',
    'driver/Set-Usb2XchangeInterfaceGuid.ps1'
)

function Assert-SafeUserRoot {
    if ([string]::IsNullOrWhiteSpace($user) -or
        [IO.Path]::GetPathRoot($user).TrimEnd('\') -eq $user -or
        $user.Length -lt 12) {
        throw "Unsafe USB2Xchange user root: $user"
    }
    $current = $user
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -Force -LiteralPath $current
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne
                    0) {
                throw "USB2Xchange user root contains a reparse point: $current"
            }
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }
}

function Get-PackageManifest {
    param([string]$Root)

    $manifestPath = Join-Path $Root 'USB2XCHANGE-RUNTIME-PACKAGE.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        ((Get-Item -LiteralPath $manifestPath).Attributes -band
            [IO.FileAttributes]::ReparsePoint)) {
        throw 'The hashed USB2Xchange package manifest is missing.'
    }
    $manifest = Get-Content -Raw -LiteralPath $manifestPath |
        ConvertFrom-Json
    if ($manifest.Schema -ne 1 -or
        $manifest.PackageType -ne 'PortableUserModeRuntime' -or
        $manifest.Commit -notmatch '^[0-9a-f]{40}$' -or
        $null -eq $manifest.Files -or $manifest.Files.Count -ne 46) {
        throw 'The USB2Xchange package manifest is invalid.'
    }
    $seen = @{}
    foreach ($record in $manifest.Files) {
        $recordPath = ([string]$record.Path).Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($recordPath) -or
            $recordPath -match '(^|/)\.\.(/|$)' -or
            $recordPath -match '^/' -or $recordPath -match '^[A-Za-z]:' -or
            [string]$record.Sha256 -notmatch '^[0-9A-F]{64}$' -or
            [int64]$record.Length -lt 0 -or $seen.ContainsKey($recordPath)) {
            throw 'The USB2Xchange package manifest contains an invalid record.'
        }
        $seen[$recordPath] = $true
        $path = [IO.Path]::GetFullPath((Join-Path $Root $recordPath))
        if (-not $path.StartsWith($Root + '\',
                [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $path -PathType Leaf) -or
            ((Get-Item -LiteralPath $path).Attributes -band
                [IO.FileAttributes]::ReparsePoint)) {
            throw "Packaged file is missing or unsafe: $($record.Path)"
        }
        $item = Get-Item -LiteralPath $path
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
        if ($item.Length -ne [int64]$record.Length -or
            $actual -ne [string]$record.Sha256) {
            throw "Packaged file failed validation: $($record.Path)"
        }
    }
    foreach ($requiredPath in $requiredPackagePaths) {
        if (-not $seen.ContainsKey($requiredPath)) {
            throw "The USB2Xchange package is missing: $requiredPath"
        }
    }
    return $manifest
}

function Invoke-CapturedNative {
    param(
        [string]$Executable,
        [string[]]$Arguments
    )

    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $Executable @Arguments 2>&1 |
            ForEach-Object { "$_" })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedPreference
    }
    return [pscustomobject][ordered]@{
        ExitCode = $exitCode
        Output = [string[]]$output
    }
}

function Get-Configuration {
    if (-not (Test-Path -LiteralPath $configurationPath -PathType Leaf)) {
        return $null
    }
    $configuration = Get-Content -Raw -LiteralPath $configurationPath |
        ConvertFrom-Json
    if ($configuration.Schema -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$configuration.InstallRoot) -or
        [string]::IsNullOrWhiteSpace([string]$configuration.SourceRoot) -or
        [string]::IsNullOrWhiteSpace(
            [string]$configuration.AdapterFirmwarePath)) {
        throw 'The USB2Xchange configuration file is invalid.'
    }
    $configuredInstall = [IO.Path]::GetFullPath(
        [string]$configuration.InstallRoot).TrimEnd('\')
    if (-not $configuredInstall.Equals($install,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The USB2Xchange configuration points outside its install root.'
    }
    return $configuration
}

function Write-Configuration {
    param(
        [string]$ConfiguredSourceRoot,
        [string]$ConfiguredLegacyRuntimeRoot
    )

    $configuration = [pscustomobject][ordered]@{
        Schema = 1
        InstallRoot = $install
        SourceRoot = $ConfiguredSourceRoot
        AdapterFirmwarePath = $storedFirmwarePath
        LegacyRuntimeRoot = $ConfiguredLegacyRuntimeRoot
        InstalledUtc = [DateTime]::UtcNow.ToString('O')
    }
    New-Item -ItemType Directory -Path $user -Force | Out-Null
    $temporary = $configurationPath + '.new'
    $configuration | ConvertTo-Json -Depth 3 |
        Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $configurationPath -Force
}

function Get-WorkflowPath {
    param([object]$Configuration)

    $path = Join-Path ([string]$Configuration.InstallRoot) `
        'scripts\Usb2Xchange-FlexColor.ps1'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'The installed USB2Xchange workflow is missing. Reinstall the package.'
    }
    return $path
}

function Invoke-Workflow {
    param(
        [object]$Configuration,
        [string]$WorkflowAction,
        [switch]$WorkflowJson
    )

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', (Get-WorkflowPath $Configuration),
        '-Action', $WorkflowAction,
        '-SourceRoot', [string]$Configuration.SourceRoot,
        '-PrivateRoot', (Join-Path ([string]$Configuration.InstallRoot) `
            'out\flexcolor-aspi-private'),
        '-AdapterFirmwarePath', [string]$Configuration.AdapterFirmwarePath
    )
    if (-not [string]::IsNullOrWhiteSpace(
            [string]$Configuration.LegacyRuntimeRoot)) {
        $arguments += @(
            '-LegacyRuntimeRoot', [string]$Configuration.LegacyRuntimeRoot)
    }
    if ($WorkflowJson) {
        $arguments += '-Json'
    }
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(powershell.exe @arguments 2>&1 |
            ForEach-Object { "$_" })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedPreference
    }
    if ($exitCode -ne 0) {
        throw (($output -join [Environment]::NewLine).Trim())
    }
    return [string[]]$output
}

function Get-DeviceBindingState {
    $devices = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
        Where-Object {
            $_.InstanceId -like 'USB\VID_03F3&PID_2002\*' -or
            $_.InstanceId -like 'USB\VID_03F3&PID_2003\*'
        })
    if ($devices.Count -eq 0) {
        return [pscustomobject][ordered]@{
            State = 'Disconnected'
            ProductId = $null
            FriendlyName = $null
            InstanceId = $null
            Service = $null
            InterfaceRegistered = $false
            Guidance = 'Connect the USB2Xchange adapter.'
        }
    }
    if ($devices.Count -ne 1) {
        return [pscustomobject][ordered]@{
            State = 'Ambiguous'
            ProductId = $null
            FriendlyName = $null
            InstanceId = $null
            Service = $null
            InterfaceRegistered = $false
            Guidance = 'Exactly one USB2Xchange must be connected during setup.'
        }
    }
    $device = $devices[0]
    $productId = if ($device.InstanceId -like '*PID_2002*') {
        '2002'
    }
    else {
        '2003'
    }
    $serviceProperty = Get-PnpDeviceProperty -InstanceId $device.InstanceId `
        -KeyName 'DEVPKEY_Device_Service' -ErrorAction SilentlyContinue
    $service = if ($null -eq $serviceProperty) {
        ''
    }
    else {
        [string]$serviceProperty.Data
    }
    $registryPath = 'Registry::HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\' +
        $device.InstanceId + '\Device Parameters'
    $registered = $false
    if (Test-Path -LiteralPath $registryPath) {
        $property = Get-ItemProperty -LiteralPath $registryPath `
            -Name 'DeviceInterfaceGUIDs' -ErrorAction SilentlyContinue
        if ($null -ne $property) {
            $registered = @($property.DeviceInterfaceGUIDs) -contains $interfaceGuid
        }
    }
    $state = 'Ready'
    $guidance = "PID $productId is ready for USB2Xchange."
    if ($service -ne 'WinUSB') {
        $state = 'NeedsWinUsb'
        $guidance = (
            "PID $productId needs the Microsoft WinUsb Device driver. " +
            'Open Device Manager, choose Update driver, Browse my computer, ' +
            'Let me pick, Universal Serial Bus devices, WinUsb Device.')
    }
    elseif (-not $registered) {
        $state = 'NeedsInterfaceRegistration'
        $guidance = (
            "PID $productId uses WinUSB but needs one elevated interface " +
            'registration. Click Register interface.')
    }
    return [pscustomobject][ordered]@{
        State = $state
        ProductId = $productId
        FriendlyName = [string]$device.FriendlyName
        InstanceId = [string]$device.InstanceId
        Service = $service
        InterfaceRegistered = $registered
        Guidance = $guidance
    }
}

function Write-DeviceStatus {
    $device = Get-DeviceBindingState
    if ($Json) {
        $device | ConvertTo-Json -Depth 3
        return
    }
    Write-Output "Adapter state: $($device.State)"
    if (-not [string]::IsNullOrWhiteSpace([string]$device.ProductId)) {
        Write-Output "Product ID:    $($device.ProductId)"
        Write-Output "Windows driver: $($device.Service)"
    }
    Write-Output $device.Guidance
}

function Install-PackageFiles {
    $manifest = Get-PackageManifest $packageRoot
    if (Test-Path -LiteralPath $install) {
        throw (
            "USB2Xchange is already installed at $install. Use Repair or " +
            'Uninstall from the installed manager.')
    }
    $staging = Join-Path $user ('.install-' + [Guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        foreach ($record in $manifest.Files) {
            $source = Join-Path $packageRoot ([string]$record.Path)
            $destination = Join-Path $staging ([string]$record.Path)
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) `
                -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination
        }
        Copy-Item -LiteralPath (Join-Path $packageRoot `
            'USB2XCHANGE-RUNTIME-PACKAGE.json') -Destination $staging
        Get-PackageManifest $staging | Out-Null
        Move-Item -LiteralPath $staging -Destination $install
    }
    finally {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Recurse -Force
        }
    }
}

function New-EndUserShortcuts {
    param([switch]$SkipDesktop)

    $manager = Join-Path $install 'USB2Xchange.exe'
    if (-not (Test-Path -LiteralPath $manager -PathType Leaf)) {
        throw 'The installed USB2Xchange manager is missing.'
    }
    $shell = New-Object -ComObject WScript.Shell
    $startMenu = Join-Path $env:APPDATA `
        'Microsoft\Windows\Start Menu\Programs\USB2Xchange'
    New-Item -ItemType Directory -Path $startMenu -Force | Out-Null

    $managerShortcut = $shell.CreateShortcut(
        (Join-Path $startMenu 'USB2Xchange Manager.lnk'))
    $managerShortcut.TargetPath = $manager
    $managerShortcut.WorkingDirectory = $install
    $managerShortcut.Description = 'Configure and diagnose USB2Xchange'
    $managerShortcut.Save()

    $flexColorShortcut = $shell.CreateShortcut(
        (Join-Path $startMenu 'FlexColor with USB2Xchange.lnk'))
    $flexColorShortcut.TargetPath = $manager
    $flexColorShortcut.Arguments = '--start'
    $flexColorShortcut.WorkingDirectory = $install
    $flexColorShortcut.Description = 'Initialize USB2Xchange and start FlexColor'
    $flexColorShortcut.Save()

    if (-not $SkipDesktop) {
        $desktop = [Environment]::GetFolderPath('DesktopDirectory')
        $desktopShortcut = $shell.CreateShortcut(
            (Join-Path $desktop 'FlexColor with USB2Xchange.lnk'))
        $desktopShortcut.TargetPath = $manager
        $desktopShortcut.Arguments = '--start'
        $desktopShortcut.WorkingDirectory = $install
        $desktopShortcut.Description =
            'Initialize USB2Xchange and start FlexColor'
        $desktopShortcut.Save()
    }

    $uninstallKey =
        'Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\USB2Xchange'
    New-Item -Path $uninstallKey -Force | Out-Null
    $uninstallArguments =
        '-NoProfile -ExecutionPolicy Bypass -File "' +
        (Join-Path $install 'scripts\Usb2Xchange-EndUser.ps1') +
        '" -Action Uninstall'
    New-ItemProperty -LiteralPath $uninstallKey -Name DisplayName `
        -Value 'USB2Xchange for FlexColor' -PropertyType String -Force |
        Out-Null
    New-ItemProperty -LiteralPath $uninstallKey -Name DisplayVersion `
        -Value '1.0' -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $uninstallKey -Name Publisher `
        -Value 'USB2Xchange Community Project' -PropertyType String -Force |
        Out-Null
    New-ItemProperty -LiteralPath $uninstallKey -Name InstallLocation `
        -Value $install -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $uninstallKey -Name DisplayIcon `
        -Value (Join-Path $install 'USB2Xchange.exe') `
        -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $uninstallKey -Name UninstallString `
        -Value "powershell.exe $uninstallArguments" `
        -PropertyType String -Force | Out-Null
    New-ItemProperty -LiteralPath $uninstallKey -Name NoModify `
        -Value 1 -PropertyType DWord -Force | Out-Null
}

function Remove-EndUserShortcuts {
    $paths = @(
        (Join-Path $env:APPDATA `
            'Microsoft\Windows\Start Menu\Programs\USB2Xchange'),
        (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) `
            'FlexColor with USB2Xchange.lnk')
    )
    foreach ($path in $paths) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
    $uninstallKey =
        'Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\USB2Xchange'
    if (Test-Path -LiteralPath $uninstallKey) {
        Remove-Item -LiteralPath $uninstallKey -Recurse -Force
    }
}

function Write-EndUserStatus {
    $configuration = Get-Configuration
    if ($null -eq $configuration) {
        $status = [pscustomobject][ordered]@{
            Schema = 1
            Installation = 'NotInstalled'
            InstallRoot = $install
            Application = 'Run setup to install USB2Xchange for this user.'
            Adapter = (Get-DeviceBindingState).State
            ReadyToStart = $false
        }
    }
    else {
        $workflowText = Invoke-Workflow $configuration 'Status' -WorkflowJson
        $workflow = ($workflowText -join [Environment]::NewLine) |
            ConvertFrom-Json
        $device = Get-DeviceBindingState
        $application = if ([string]$workflow.ManagedProcess -like 'Running:*') {
            'FlexColorRunning'
        }
        elseif ($workflow.ReadyForOperatorStart) {
            'Ready'
        }
        else {
            'NeedsRepair'
        }
        $status = [pscustomobject][ordered]@{
            Schema = 1
            Installation = 'Installed'
            InstallRoot = [string]$configuration.InstallRoot
            Application = $application
            Adapter = $device.State
            AdapterProductId = $device.ProductId
            PrivateState = $workflow.PrivateState
            ManagedProcess = $workflow.ManagedProcess
            ReadyToStart = [bool]$workflow.ReadyForOperatorStart
        }
    }
    if ($Json) {
        $status | ConvertTo-Json -Depth 4
        return
    }
    if ($status.Installation -eq 'NotInstalled') {
        Write-Output 'USB2Xchange is not installed for this Windows user.'
        Write-Output "Install location: $($status.InstallRoot)"
    }
    else {
        Write-Output "USB2Xchange application: $($status.Application)"
        Write-Output "Private FlexColor:       $($status.PrivateState)"
        Write-Output "Managed process:         $($status.ManagedProcess)"
    }
    Write-Output "Adapter:                 $($status.Adapter)"
}

Assert-SafeUserRoot

# Version only: no identity, hardware IDs, timestamp, or telemetry is stored.
$riskPath = Join-Path $user 'risk-acknowledgement.txt'
$riskVersion = 'fcusb-experimental-risk-v1'
if ($Action -eq 'AcknowledgeRisk') {
    New-Item -ItemType Directory -Path $user -Force | Out-Null
    if ((Test-Path -LiteralPath $riskPath) -and
        ((Get-Item -LiteralPath $riskPath).Attributes -band
            [IO.FileAttributes]::ReparsePoint)) {
        throw 'Unsafe risk acknowledgement path.'
    }
    [IO.File]::WriteAllText($riskPath, $riskVersion, [Text.Encoding]::ASCII)
    Write-Output 'Experimental hardware risks acknowledged locally.'
    exit 0
}
if ($Action -in @('Start', 'InitializeAdapter', 'RegisterPresentInterface')) {
    if (-not (Test-Path -LiteralPath $riskPath -PathType Leaf) -or
        ((Get-Item -LiteralPath $riskPath).Attributes -band
            [IO.FileAttributes]::ReparsePoint) -or
        (Get-Item -LiteralPath $riskPath).Length -ne $riskVersion.Length -or
        [IO.File]::ReadAllText($riskPath) -cne $riskVersion) {
        throw 'Experimental risk acknowledgement required. Open the manager, read the warning, and explicitly accept before hardware operation.'
    }
}

switch ($Action) {
    'Status' {
        Write-EndUserStatus
    }
    'DeviceStatus' {
        Write-DeviceStatus
    }
    'Install' {
        if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
            $SourceRoot =
                'C:\Program Files (x86)\Hasselblad\FlexColor English v4.0.3'
        }
        if ([string]::IsNullOrWhiteSpace($AdapterFirmwarePath)) {
            throw 'Select the locally supplied USB2Xchange firmware file.'
        }
        $source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
        $firmware = [IO.Path]::GetFullPath($AdapterFirmwarePath)
        if (-not (Test-Path -LiteralPath $firmware -PathType Leaf)) {
            throw "Adapter firmware was not found: $firmware"
        }
        $firmwareItem = Get-Item -LiteralPath $firmware
        $firmwareHash =
            (Get-FileHash -Algorithm SHA256 -LiteralPath $firmware).Hash
        if ($firmwareItem.Length -ne 16492 -or
            $firmwareHash -ne $expectedFirmwareHash) {
            throw 'The selected adapter firmware is not the supported image.'
        }
        $legacy = if ([string]::IsNullOrWhiteSpace($LegacyRuntimeRoot)) {
            $null
        }
        else {
            [IO.Path]::GetFullPath($LegacyRuntimeRoot).TrimEnd('\')
        }
        Install-PackageFiles
        try {
            New-Item -ItemType Directory -Path $firmwareDirectory -Force |
                Out-Null
            Copy-Item -LiteralPath $firmware -Destination $storedFirmwarePath `
                -Force
            $configuration = [pscustomobject][ordered]@{
                InstallRoot = $install
                SourceRoot = $source
                AdapterFirmwarePath = $storedFirmwarePath
                LegacyRuntimeRoot = $legacy
            }
            Invoke-Workflow $configuration 'Prepare' | Write-Output
            Write-Configuration -ConfiguredSourceRoot $source `
                -ConfiguredLegacyRuntimeRoot $legacy
            if (-not $NoShellIntegration) {
                New-EndUserShortcuts -SkipDesktop:$NoDesktopShortcut
            }
        }
    catch {
        if (-not $NoShellIntegration) {
            try {
                Remove-EndUserShortcuts
            }
            catch {
                # Preserve the original setup error.
            }
        }
        if (Test-Path -LiteralPath $install) {
            Remove-Item -LiteralPath $install -Recurse -Force
        }
        foreach ($partial in @($configurationPath, $firmwareDirectory)) {
            if (Test-Path -LiteralPath $partial) {
                Remove-Item -LiteralPath $partial -Recurse -Force
            }
        }
        if ((Test-Path -LiteralPath $user) -and
            @(Get-ChildItem -Force -LiteralPath $user).Count -eq 0) {
            Remove-Item -LiteralPath $user -Force
        }
        throw
        }
        Write-Output 'USB2Xchange was installed and FlexColor was prepared.'
        Write-Output 'Use Adapter Setup once for PID 2002 and PID 2003.'
    }
    'Repair' {
        $configuration = Get-Configuration
        if ($null -eq $configuration) {
            throw 'USB2Xchange is not installed. Run Setup first.'
        }
        Invoke-Workflow $configuration 'Prepare' | Write-Output
        if (-not $NoShellIntegration) {
            New-EndUserShortcuts -SkipDesktop:$NoDesktopShortcut
        }
        Write-Output 'USB2Xchange and the private FlexColor copy were repaired.'
    }
    'Start' {
        $configuration = Get-Configuration
        if ($null -eq $configuration) {
            throw 'USB2Xchange is not installed. Run Setup first.'
        }
        Invoke-Workflow $configuration 'Start' | Write-Output
    }
    'Stop' {
        $configuration = Get-Configuration
        if ($null -eq $configuration) {
            throw 'USB2Xchange is not installed.'
        }
        Invoke-Workflow $configuration 'Stop' | Write-Output
    }
    'RegisterPresentInterface' {
        $device = Get-DeviceBindingState
        if ($device.State -ne 'NeedsInterfaceRegistration') {
            throw "The present adapter is not ready for registration: $($device.State)"
        }
        $helper = Join-Path $install `
            'driver\Set-Usb2XchangeInterfaceGuid.ps1'
        if (-not (Test-Path -LiteralPath $helper -PathType Leaf)) {
            throw 'The installed interface-registration helper is missing.'
        }
        & $helper -InstanceId $device.InstanceId
        $restart = Start-Process -FilePath (Join-Path $env:WINDIR `
            'System32\pnputil.exe') -ArgumentList @(
                '/restart-device', $device.InstanceId) -Wait -PassThru `
            -WindowStyle Hidden
        if ($restart.ExitCode -ne 0) {
            Write-Output 'Disconnect and reconnect the adapter to apply the interface.'
        }
        else {
            Write-Output 'The adapter interface was registered and restarted.'
        }
    }
    'InitializeAdapter' {
        $configuration = Get-Configuration
        if ($null -eq $configuration) {
            throw 'USB2Xchange is not installed.'
        }
        $device = Get-DeviceBindingState
        if ($device.State -ne 'Ready' -or $device.ProductId -ne '2002') {
            throw (
                'Connect a ready PID-2002 adapter before initialization. ' +
                "Current state: $($device.State), PID $($device.ProductId)")
        }
        $diagnostic = Join-Path $install 'out\bin\usb2xchange.exe'
        $native = Invoke-CapturedNative -Executable $diagnostic -Arguments @(
            'load', [string]$configuration.AdapterFirmwarePath,
            '--reenumeration-seconds', '60')
        $native.Output | Write-Output
        if ($native.ExitCode -ne 0) {
            $after = Get-DeviceBindingState
            if ($after.ProductId -eq '2003') {
                if ($after.State -eq 'NeedsWinUsb') {
                    throw (
                        'Firmware loaded and PID 2003 appeared. Bind this second ' +
                        'device to Microsoft WinUsb Device, then register its interface.')
                }
                if ($after.State -eq 'NeedsInterfaceRegistration') {
                    throw (
                        'Firmware loaded and PID 2003 appeared with WinUSB. ' +
                        'Click Register interface for this second adapter identity.')
                }
                if ($after.State -eq 'Ready') {
                    Write-Output (
                        'PID 2003 is ready. Windows completed the firmware ' +
                        'transition after the loader wait ended.')
                    break
                }
            }
            throw (
                "Adapter initialization failed with exit code " +
                "$($native.ExitCode).")
        }
    }
    'OpenLogs' {
        $configuration = Get-Configuration
        if ($null -eq $configuration) {
            throw 'USB2Xchange is not installed.'
        }
        $logs = Join-Path ([string]$configuration.InstallRoot) `
            'out\flexcolor-aspi-private\Usb2XchangeLogs'
        New-Item -ItemType Directory -Path $logs -Force | Out-Null
        Start-Process -FilePath explorer.exe -ArgumentList @($logs)
    }
    'Uninstall' {
        if ($WaitForProcessId -gt 0) {
            $deadline = [DateTime]::UtcNow.AddSeconds(15)
            while ((Get-Process -Id $WaitForProcessId `
                        -ErrorAction SilentlyContinue) -and
                [DateTime]::UtcNow -lt $deadline) {
                Start-Sleep -Milliseconds 200
            }
        }
        $configuration = Get-Configuration
        if ($null -ne $configuration) {
            $statusText = Invoke-Workflow $configuration 'Status' -WorkflowJson
            $status = ($statusText -join [Environment]::NewLine) |
                ConvertFrom-Json
            if ([string]$status.ManagedProcess -like 'Running:*') {
                throw 'Close or Stop FlexColor before uninstalling USB2Xchange.'
            }
            Invoke-Workflow $configuration 'Deactivate' | Write-Output
        }
        if (-not $NoShellIntegration) {
            Remove-EndUserShortcuts
        }
        Set-Location $env:TEMP
        if (Test-Path -LiteralPath $user) {
            Remove-Item -LiteralPath $user -Recurse -Force
        }
        Write-Output 'USB2Xchange was removed for this Windows user.'
        Write-Output (
            'The inbox WinUSB selections were left intact; they contain no ' +
            'project kernel driver and can be changed in Device Manager.')
    }
}
