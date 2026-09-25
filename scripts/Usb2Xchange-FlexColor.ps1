# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [ValidateSet('Status', 'Prepare', 'Start', 'Stop', 'Deactivate')]
    [string]$Action = 'Status',

    [string]$SourceRoot =
        'C:\Program Files (x86)\Hasselblad\FlexColor English v4.0.3',

    [string]$PrivateRoot,

    [string]$LegacyRuntimeRoot,

    [string]$AdapterFirmwarePath,

    [switch]$Json,

    [ValidateRange(0, 60)]
    [int]$ForceAfterSeconds = 10
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'out')).TrimEnd('\')
if ([string]::IsNullOrWhiteSpace($PrivateRoot)) {
    $PrivateRoot = Join-Path $outputRoot 'flexcolor-aspi-private'
}
$private = [IO.Path]::GetFullPath($PrivateRoot).TrimEnd('\')
$source = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
if (-not $private.StartsWith($outputRoot + '\',
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "PrivateRoot must remain under the ignored output tree: $outputRoot"
}

function Assert-SafePrivatePath {
    $current = $private
    while (-not [string]::IsNullOrEmpty($current)) {
        if (Test-Path -LiteralPath $current) {
            $attributes = (Get-Item -Force -LiteralPath $current).Attributes
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "PrivateRoot contains a reparse point: $current"
            }
        }
        if ($current.Equals($outputRoot,
                [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) {
            break
        }
        $current = $parent
    }
}

Assert-SafePrivatePath

function Invoke-CapturedNativeCommand {
    param(
        [string]$Executable,
        [string[]]$Arguments
    )

    # Windows PowerShell 5.1 converts redirected native stderr into
    # non-terminating ErrorRecord objects. With the workflow's fail-closed
    # ErrorActionPreference=Stop, even an informational native log line would
    # otherwise terminate the pipeline before the process exit code is read.
    $savedErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $captured = @(& $Executable @Arguments 2>&1 |
            ForEach-Object { "$_" })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
    }
    return [pscustomobject][ordered]@{
        ExitCode = $exitCode
        Output = [string[]]$captured
    }
}

$expectedExe =
    '4C5D402F3668F06BEAFC871B9F152D55BF5ACCA191C43082C6A8C5647916AF28'
$expectedOriginalDll =
    'B49217BA2BBFF2E9A9DF0952CC9657CD197C10022E2B62E3B818719FB78C1E84'
$expectedPatchedDll =
    'D250B6177D2B30FADD55E06DF612E7F534CB5D1A30E82AA9CC71A3F1924DD119'
$expectedScannerFirmware =
    'D8D7188574C52B255CF7940BEF7CC9192F744693AABC1F735758B7FECDEC65F3'
$expectedAdapterFirmware =
    'D0967EF81E71E9293D0499C91D687E2409F8CA13B14FFE2C4F35F07685D25FBD'
$legacyRuntimeFiles = [ordered]@{
    'MFC71.dll' =
        '4DA5EFDC46D126B45DAEEE8BC69C0BA2AA243589046B7DFD12A7E21B9BEE6A32'
    'MSVCR71.dll' =
        '8094AF5EE310714CAEBCCAEEE7769FFB08048503BA478B879EDFEF5F1A24FEFE'
    'MSVCP71.dll' =
        'DF96156F6A548FD6FE5672918DE5AE4509D3C810A57BFFD2A91DE45A3ED5B23B'
}
$stageNames = @(
    'wnaspi32.dll',
    'wnaspi32.dll.config',
    'Usb2Xchange.AspiShim.Managed.dll',
    'Usb2Xchange.WinUsb.dll',
    'Usb2Xchange.Protocol.dll',
    'FlexColor.exe.config'
)
$sessionPath = Join-Path $private '.usb2xchange-operator-session.json'

function Get-HashState {
    param(
        [string]$Path,
        [string]$ExpectedHash
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 'Missing'
    }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    if ($actual -eq $ExpectedHash) {
        return 'Supported'
    }
    return "HashMismatch:$actual"
}

function Get-PatchState {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 'Missing'
    }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    if ($actual -eq $expectedOriginalDll) {
        return 'Inactive'
    }
    if ($actual -eq $expectedPatchedDll) {
        return 'Active'
    }
    return "Unexpected:$actual"
}

function Get-ScannerFirmwareState {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 'Missing'
    }
    $file = Get-Item -LiteralPath $Path
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    if ($file.Length -eq 65536 -and $actual -eq $expectedScannerFirmware) {
        return 'Supported'
    }
    return "Mismatch:Length=$($file.Length),SHA256=$actual"
}

function Get-AdapterFirmwareState {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return 'NotConfigured'
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 'Missing'
    }
    $file = Get-Item -LiteralPath $Path
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
    if ($file.Length -eq 16492 -and $actual -eq $expectedAdapterFirmware) {
        return 'Supported'
    }
    return "Mismatch:Length=$($file.Length),SHA256=$actual"
}

function Get-LegacyRuntimeState {
    param([string]$Root)

    if ([string]::IsNullOrWhiteSpace($Root) -or
        -not (Test-Path -LiteralPath $Root -PathType Container)) {
        return 'Missing:' + (($legacyRuntimeFiles.Keys) -join ',')
    }
    $missing = @()
    $mismatched = @()
    foreach ($entry in $legacyRuntimeFiles.GetEnumerator()) {
        $path = Join-Path $Root $entry.Key
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $missing += $entry.Key
        }
        elseif ((Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash -ne
                $entry.Value) {
            $mismatched += $entry.Key
        }
    }
    if ($missing.Count -ne 0) {
        return 'Missing:' + ($missing -join ',')
    }
    if ($mismatched.Count -ne 0) {
        return 'HashMismatch:' + ($mismatched -join ',')
    }
    return 'Supported'
}

function Convert-SessionTimeToUtc {
    param([object]$Value)

    if ($Value -is [DateTimeOffset]) {
        return ([DateTimeOffset]$Value).UtcDateTime
    }
    if ($Value -is [DateTime]) {
        return ([DateTime]$Value).ToUniversalTime()
    }
    $parsed = [DateTimeOffset]::Parse(
        [string]$Value,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind)
    return $parsed.UtcDateTime
}

function Get-ManagedProcessState {
    if (-not (Test-Path -LiteralPath $sessionPath -PathType Leaf)) {
        return 'None'
    }
    try {
        $session = Get-Content -Raw -LiteralPath $sessionPath |
            ConvertFrom-Json
        if ($session.Schema -ne 1 -or $session.ProcessId -lt 1 -or
            [string]::IsNullOrWhiteSpace([string]$session.Executable) -or
            [string]::IsNullOrWhiteSpace([string]$session.StartTimeUtc)) {
            return 'InvalidSessionRecord'
        }
        $process = Get-Process -Id ([int]$session.ProcessId) `
            -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            return 'StaleSessionRecord'
        }
        try {
            $expectedPath = [IO.Path]::GetFullPath(
                [string]$session.Executable)
            $actualPath = [IO.Path]::GetFullPath($process.Path)
            $expectedStart = Convert-SessionTimeToUtc $session.StartTimeUtc
            $delta = [Math]::Abs(
                ($process.StartTime.ToUniversalTime() - $expectedStart).
                    TotalSeconds)
            if (-not $actualPath.Equals($expectedPath,
                    [StringComparison]::OrdinalIgnoreCase) -or
                $delta -gt 2) {
                return 'OwnershipMismatch'
            }
            return "Running:$($process.Id)"
        }
        finally {
            $process.Dispose()
        }
    }
    catch {
        return "InvalidSessionRecord:$($_.Exception.Message)"
    }
}

function Get-WorkflowStatus {
    $sourceExe = Join-Path $source 'FlexColor.exe'
    $sourceDll = Join-Path $source 'DLLS\FlexColor.dll'
    $privateExe = Join-Path $private 'FlexColor.exe'
    $privateDll = Join-Path $private 'DLLS\FlexColor.dll'
    $backupDll = $privateDll + '.usb2xchange-original'
    $scannerFirmware = Join-Path $private 'Firmware\MICROCOD.3XX'
    $adapterFirmware = if ([string]::IsNullOrWhiteSpace(
            $AdapterFirmwarePath)) {
        $null
    }
    else {
        [IO.Path]::GetFullPath($AdapterFirmwarePath)
    }

    $sourceExeState = Get-HashState $sourceExe $expectedExe
    $sourceDllState = Get-HashState $sourceDll $expectedOriginalDll
    $privateExeState = Get-HashState $privateExe $expectedExe
    $patchState = Get-PatchState $privateDll
    $backupState = Get-HashState $backupDll $expectedOriginalDll
    $scannerFirmwareState = Get-ScannerFirmwareState $scannerFirmware
    $adapterFirmwareState = Get-AdapterFirmwareState $adapterFirmware
    $legacyRuntimeState = Get-LegacyRuntimeState $private

    $stageState = 'Missing'
    if (Test-Path -LiteralPath $private -PathType Container) {
        $missing = @()
        $stale = @()
        foreach ($name in $stageNames) {
            $built = Join-Path $repoRoot "out\aspi\$name"
            $staged = Join-Path $private $name
            if (-not (Test-Path -LiteralPath $staged -PathType Leaf)) {
                $missing += $name
            }
            elseif (-not (Test-Path -LiteralPath $built -PathType Leaf) -or
                (Get-FileHash -Algorithm SHA256 -LiteralPath $built).Hash -ne
                (Get-FileHash -Algorithm SHA256 -LiteralPath $staged).Hash) {
                $stale += $name
            }
        }
        if ($missing.Count -ne 0) {
            $stageState = 'Missing:' + ($missing -join ',')
        }
        elseif ($stale.Count -ne 0) {
            $stageState = 'Stale:' + ($stale -join ',')
        }
        else {
            $stageState = 'Current'
        }
    }

    $privateState = 'Missing'
    if (Test-Path -LiteralPath $private -PathType Container) {
        $privateState = if ($privateExeState -eq 'Supported' -and
            $patchState -eq 'Active' -and
            $backupState -eq 'Supported' -and
            $scannerFirmwareState -eq 'Supported' -and
            $legacyRuntimeState -eq 'Supported' -and
            $stageState -eq 'Current') {
            'Prepared'
        }
        else {
            'NeedsPreparation'
        }
    }

    $managedProcessState = Get-ManagedProcessState
    $readyForOperatorStart = $privateState -eq 'Prepared' -and
        ($managedProcessState -eq 'None' -or
         $managedProcessState -eq 'StaleSessionRecord')

    return [pscustomobject][ordered]@{
        Schema = 1
        SourceRoot = $source
        SourceExecutable = $sourceExeState
        SourceDll = $sourceDllState
        PrivateRoot = $private
        PrivateState = $privateState
        PrivateExecutable = $privateExeState
        PatchState = $patchState
        BackupState = $backupState
        LegacyRuntime = $legacyRuntimeState
        AdapterFirmwarePath = $adapterFirmware
        AdapterFirmware = $adapterFirmwareState
        ScannerFirmware = $scannerFirmwareState
        ProviderStaging = $stageState
        ManagedProcess = $managedProcessState
        Hardware = 'NotChecked'
        OperatorRuntime = 'TrustedFlexColorPassThroughHardwareAccepted'
        ReadyForOperatorStart = $readyForOperatorStart
    }
}

function Write-WorkflowStatus {
    $status = Get-WorkflowStatus
    if ($Json) {
        $status | ConvertTo-Json -Depth 3
        return
    }
    $status | Format-List
    Write-Output 'Status is offline-only; no adapter or scanner was opened.'
    Write-Output (
        'Operator Start uses the hardware-accepted exact-hash trusted ' +
        'transparent pass-through. Start performs its own adapter/scanner ' +
        'preflight and records only the process it launches.')
}

function Get-PresentUsb2XchangeDevices {
    return @(Get-PnpDevice -PresentOnly -ErrorAction Stop |
        Where-Object {
            $_.InstanceId -like 'USB\VID_03F3&PID_2002*' -or
            $_.InstanceId -like 'USB\VID_03F3&PID_2003*'
        })
}

function Require-OperationalAdapter {
    $devices = @(Get-PresentUsb2XchangeDevices)
    $loader = @($devices | Where-Object {
            $_.InstanceId -like 'USB\VID_03F3&PID_2002*'
        })
    $operational = @($devices | Where-Object {
            $_.InstanceId -like 'USB\VID_03F3&PID_2003*'
        })
    if ($loader.Count -gt 1 -or $operational.Count -gt 1 -or
        ($loader.Count -ne 0 -and $operational.Count -ne 0)) {
        throw (
            'USB2Xchange enumeration is ambiguous. Disconnect duplicate ' +
            'adapters and retry.')
    }
    if ($operational.Count -eq 1) {
        if ($operational[0].Status -ne 'OK') {
            throw 'USB2Xchange PID 2003 is present but not healthy.'
        }
        Write-Output 'USB2Xchange is operational as PID 2003; firmware upload skipped.'
        return
    }
    if ($loader.Count -eq 0) {
        throw (
            'USB2Xchange was not found. Connect it and confirm WinUSB is ' +
            'bound to PID 2002 or PID 2003.')
    }
    if ($loader[0].Status -ne 'OK') {
        throw 'USB2Xchange PID 2002 is present but not healthy.'
    }

    $adapterFirmware = if ([string]::IsNullOrWhiteSpace(
            $AdapterFirmwarePath)) {
        $null
    }
    else {
        [IO.Path]::GetFullPath($AdapterFirmwarePath)
    }
    $adapterFirmwareState = Get-AdapterFirmwareState $adapterFirmware
    if ($adapterFirmwareState -ne 'Supported') {
        throw (
            'USB2Xchange is in loader PID 2002. Retry Start with ' +
            '-AdapterFirmwarePath pointing to the exact supported local ' +
            'usb2xchange.fw (16,492 bytes; redistribution is not assumed).')
    }
    $diagnostic = Join-Path $repoRoot 'out\bin\usb2xchange.exe'
    if (-not (Test-Path -LiteralPath $diagnostic -PathType Leaf)) {
        throw 'The reviewed USB2Xchange diagnostic build is missing; run Prepare.'
    }
    $loadResult = Invoke-CapturedNativeCommand -Executable $diagnostic `
        -Arguments @('load', $adapterFirmware, '--reenumeration-seconds', '30')
    $loadOutput = @($loadResult.Output)
    if ($loadResult.ExitCode -ne 0) {
        throw (
            'USB2Xchange firmware initialization failed: ' +
            (($loadOutput | Select-Object -Last 8) -join ' | '))
    }
    $devices = @(Get-PresentUsb2XchangeDevices)
    $operational = @($devices | Where-Object {
            $_.InstanceId -like 'USB\VID_03F3&PID_2003*' -and
            $_.Status -eq 'OK'
        })
    if ($operational.Count -ne 1) {
        throw 'USB2Xchange did not settle as one healthy PID-2003 device.'
    }
    $loadOutput | Write-Output
}

function Get-SupportedScannerIdentityFromDiagnostic {
    param(
        [int]$ExitCode,
        [string[]]$Output
    )

    $text = $Output -join "`n"
    if ($ExitCode -ne 0 -or
        $text -notmatch 'Responding targets:\s+1' -or
        $text -notmatch 'Target 5, LUN 0 responded:' -or
        $text -notmatch 'Vendor:\s+Imacon' -or
        $text -notmatch 'Product:\s+(SCSI Loader|FlexTight II)' -or
        $text -notmatch 'Revision:\s+(L302|M333)') {
        return $null
    }
    if ($text -match 'Product:\s+FlexTight II' -and
        $text -match 'Revision:\s+M333') {
        return 'Imacon / FlexTight II / M333'
    }
    if ($text -match 'Product:\s+SCSI Loader' -and
        $text -match 'Revision:\s+L302') {
        return 'Imacon / SCSI Loader / L302'
    }
    return $null
}

function Require-SupportedScannerIdentity {
    $diagnostic = Join-Path $repoRoot 'out\bin\usb2xchange.exe'
    if (-not (Test-Path -LiteralPath $diagnostic -PathType Leaf)) {
        throw 'The reviewed USB2Xchange diagnostic build is missing; run Prepare.'
    }
    $scanResult = Invoke-CapturedNativeCommand -Executable $diagnostic `
        -Arguments @('scan', '--timeout-ms', '30000')
    $scanOutput = @($scanResult.Output)
    $scanExit = $scanResult.ExitCode
    $identity = Get-SupportedScannerIdentityFromDiagnostic `
        -ExitCode $scanExit -Output $scanOutput
    if ([string]::IsNullOrWhiteSpace($identity)) {
        throw (
            'The supported scanner was not found uniquely at target 5. ' +
            'Turn on the scanner, check the SCSI cable/target ID, wait for ' +
            'startup, and retry. Diagnostic tail: ' +
            (($scanOutput | Select-Object -Last 12) -join ' | '))
    }
    Write-Output "Verified scanner target 5: $identity."
}

function Get-TrustedLauncherRecordFromOutput {
    param(
        [int]$ExitCode,
        [string[]]$Output
    )

    $pidMatches = @($Output | ForEach-Object {
            if ($_ -match '^Started trusted transparent FlexColor PID ([0-9]+)\.$') {
                [int]$Matches[1]
            }
        })
    $logMatches = @($Output | ForEach-Object {
            if ($_ -match '^ASPI log: (.+)$') {
                $Matches[1]
            }
        })
    if ($ExitCode -ne 0 -or $pidMatches.Count -ne 1 -or
        $logMatches.Count -ne 1) {
        return $null
    }
    return [pscustomobject][ordered]@{
        ProcessId = $pidMatches[0]
        LogPath = $logMatches[0]
    }
}

function Start-OperatorProcess {
    $status = Get-WorkflowStatus
    if ($status.PrivateState -ne 'Prepared') {
        throw (
            'The private FlexColor copy is not prepared. Run -Action Prepare ' +
            'before Start.')
    }
    if ($status.ManagedProcess -eq 'StaleSessionRecord') {
        Remove-Item -LiteralPath $sessionPath -Force
        Write-Output 'Removed a stale managed-session record before Start.'
    }
    elseif ($status.ManagedProcess -ne 'None') {
        throw (
            'A project session already exists or cannot be trusted (' +
            $status.ManagedProcess + '); use Stop or resolve it before Start.')
    }

    Require-OperationalAdapter
    Require-SupportedScannerIdentity

    $launcher = Join-Path $repoRoot 'out\bin\flexcolor-aspi-launcher.exe'
    if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
        throw 'The exact provider launcher build is missing; run Prepare.'
    }
    $expectedExecutable = [IO.Path]::GetFullPath(
        (Join-Path $private 'FlexColor.exe'))
    $launchStartedUtc = [DateTime]::UtcNow.AddSeconds(-2)
    $launchResult = Invoke-CapturedNativeCommand -Executable $launcher `
        -Arguments @(
            'launch-trusted-flexcolor-pass-through',
            $private,
            '--approve-trusted-flexcolor-pass-through')
    $launchOutput = @($launchResult.Output)
    $launchExit = $launchResult.ExitCode
    $launchRecord = Get-TrustedLauncherRecordFromOutput `
        -ExitCode $launchExit -Output $launchOutput
    if ($null -eq $launchRecord) {
        $exactNewProcesses = @(Get-Process FlexColor `
            -ErrorAction SilentlyContinue | Where-Object {
                try {
                    [IO.Path]::GetFullPath($_.Path).Equals(
                        $expectedExecutable,
                        [StringComparison]::OrdinalIgnoreCase) -and
                    $_.StartTime.ToUniversalTime() -ge $launchStartedUtc
                }
                catch {
                    $false
                }
            })
        if ($exactNewProcesses.Count -eq 1) {
            $candidate = $exactNewProcesses[0]
            try {
                [void]$candidate.CloseMainWindow()
                if (-not $candidate.WaitForExit(10000)) {
                    $candidate.Kill()
                    [void]$candidate.WaitForExit(5000)
                }
            }
            finally {
                $candidate.Dispose()
            }
        }
        throw (
            'Trusted FlexColor launcher failed or returned an ambiguous ' +
            'process record: ' +
            (($launchOutput | Select-Object -Last 12) -join ' | '))
    }

    $process = Get-Process -Id $launchRecord.ProcessId `
        -ErrorAction SilentlyContinue
    $temporarySession = $sessionPath + '.tmp-' +
        [Guid]::NewGuid().ToString('N')
    try {
        if ($null -eq $process) {
            throw 'Trusted FlexColor exited before its session could be recorded.'
        }
        $actualExecutable = [IO.Path]::GetFullPath($process.Path)
        if (-not $actualExecutable.Equals($expectedExecutable,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The launched PID executable path did not match the private copy.'
        }
        $record = [pscustomobject][ordered]@{
            Schema = 1
            ProcessId = $process.Id
            Executable = $expectedExecutable
            StartTimeUtc = $process.StartTime.ToUniversalTime().ToString('O')
            LogPath = [IO.Path]::GetFullPath($launchRecord.LogPath)
        }
        $jsonText = $record | ConvertTo-Json
        [IO.File]::WriteAllText($temporarySession, $jsonText,
            [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporarySession, $sessionPath)
        $launchOutput | Write-Output
        Write-Output (
            "Recorded project-managed FlexColor PID $($process.Id); " +
            'use -Action Stop to close only this session.')
    }
    catch {
        if (Test-Path -LiteralPath $temporarySession) {
            Remove-Item -LiteralPath $temporarySession -Force
        }
        if ($null -ne $process -and -not $process.HasExited) {
            [void]$process.CloseMainWindow()
            if (-not $process.WaitForExit(10000)) {
                $process.Kill()
                [void]$process.WaitForExit(5000)
            }
        }
        throw
    }
    finally {
        if ($null -ne $process) {
            $process.Dispose()
        }
    }
}

function Stop-ManagedProcess {
    if (-not (Test-Path -LiteralPath $sessionPath -PathType Leaf)) {
        Write-Output 'No project-managed FlexColor session is recorded.'
        return
    }

    $session = Get-Content -Raw -LiteralPath $sessionPath | ConvertFrom-Json
    if ($session.Schema -ne 1 -or $session.ProcessId -lt 1 -or
        [string]::IsNullOrWhiteSpace([string]$session.Executable) -or
        [string]::IsNullOrWhiteSpace([string]$session.StartTimeUtc)) {
        throw (
            'The managed-session record is invalid; refusing to stop a ' +
            'process.')
    }
    $process = Get-Process -Id ([int]$session.ProcessId) `
        -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        Remove-Item -LiteralPath $sessionPath -Force
        Write-Output 'Removed a stale managed-session record; no process was stopped.'
        return
    }
    try {
        $expectedPath = [IO.Path]::GetFullPath([string]$session.Executable)
        $actualPath = [IO.Path]::GetFullPath($process.Path)
        $expectedStart = Convert-SessionTimeToUtc $session.StartTimeUtc
        $delta = [Math]::Abs(
            ($process.StartTime.ToUniversalTime() - $expectedStart).TotalSeconds)
        if (-not $actualPath.Equals($expectedPath,
                [StringComparison]::OrdinalIgnoreCase) -or $delta -gt 2) {
            throw (
                'The PID exists but its path/start time does not match the ' +
                'project session record; refusing to stop it.')
        }
        [void]$process.CloseMainWindow()
        if (-not $process.WaitForExit($ForceAfterSeconds * 1000)) {
            $process.Kill()
            if (-not $process.WaitForExit(5000)) {
                throw (
                    'The owned FlexColor process did not exit after ' +
                    'termination.')
            }
        }
        Remove-Item -LiteralPath $sessionPath -Force
        Write-Output "Stopped project-managed FlexColor PID $($process.Id)."
    }
    finally {
        $process.Dispose()
    }
}

switch ($Action) {
    'Status' {
        Write-WorkflowStatus
    }
    'Prepare' {
        & (Join-Path $repoRoot 'scripts\Prepare-FlexColorAspiTest.ps1') `
            -SourceRoot $source -PrivateRoot $private `
            -LegacyRuntimeRoot $LegacyRuntimeRoot
        Write-WorkflowStatus
    }
    'Start' {
        Start-OperatorProcess
    }
    'Stop' {
        Stop-ManagedProcess
    }
    'Deactivate' {
        $processState = Get-ManagedProcessState
        if ($processState.StartsWith('Running:',
                [StringComparison]::Ordinal)) {
            throw 'Stop the project-managed FlexColor process before deactivation.'
        }
        if ($processState -ne 'None' -and
            $processState -ne 'StaleSessionRecord') {
            throw (
                'The managed-session record cannot be trusted (' +
                $processState + '); refusing to change the private DLL.')
        }
        $privateDll = Join-Path $private 'DLLS\FlexColor.dll'
        if (-not (Test-Path -LiteralPath $privateDll -PathType Leaf)) {
            Write-Output 'No private FlexColor DLL exists; nothing was deactivated.'
            return
        }
        $patcher = Join-Path $repoRoot 'out\bin\flexcolor-aspi-patch.exe'
        if (-not (Test-Path -LiteralPath $patcher -PathType Leaf)) {
            throw 'The exact FlexColor patch utility is missing.'
        }
        & $patcher deactivate $privateDll
        if ($LASTEXITCODE -ne 0) {
            throw 'FlexColor private-copy patch deactivation failed.'
        }
    }
}
