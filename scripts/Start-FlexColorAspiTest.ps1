# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [ValidateSet('Preflight', 'Smoke', 'OperationalReplaySmoke',
        'FrameOperationalReplaySmoke', 'FullScanReplaySmoke',
        'OperatorRepeatReplaySmoke', 'OfflineCancelReplaySmoke', 'Launch',
        'LiveSmoke', 'LiveLaunch', 'LoaderPreflight', 'LoaderLaunch',
        'PreviewFingerprintSmoke', 'PreviewObserveSmoke',
        'Preview24x36SetWindowObserveSmoke',
        'Preview24x36FirstReadSmoke',
        'Preview4x5FirstReadSmoke',
        'PreviewObserveLaunch', 'PreviewFirstReadSmoke',
        'PreviewBurst8Smoke', 'PreviewStream256Smoke',
        'PreviewStream256ShortRetrySmoke',
        'PreviewNatural996ShortRetrySmoke',
        'PreviewNatural996PerRow8Smoke',
        'PreviewPoweredNatural911ObserveSmoke',
        'PreviewPoweredNatural911CleanupSmoke',
        'PreviewPoweredCancel32To96CleanupSmoke',
        'FullScanStartupFingerprintSmoke',
        'FullScanSetWindowFirstReadSmoke',
        'FullScanComplete60x60Smoke',
        'FullScanTerminalProbe60x60Smoke',
        'FullScanNatural996Complete60x60Smoke',
        'FullScanRow997Complete60x60Smoke',
        'FullScanProgressComplete60x60Smoke',
        'LiveOperatorRepeat60x60Smoke', 'TrustedPassThroughLaunch')]
    [string]$Action = 'Preflight',
    [string]$PrivateRoot,
    [ValidateSet('24x36', '60x60', '60x70', '4x5')]
    [string]$Frame,
    [switch]$ApproveScannerFirmwareStateChange,
    [switch]$ApproveScannerPreviewFingerprintObservation,
    [switch]$ApproveScannerPreviewObservation,
    [switch]$ApproveScanner24x36PreviewSetWindowObservation,
    [switch]$ApproveScanner24x36FirstImageReadObservation,
    [switch]$ApproveScanner4x5FirstImageReadObservation,
    [switch]$ApproveScannerFirstImageReadObservation,
    [switch]$ApproveScannerImageReadBurstObservation,
    [switch]$ApproveScannerImageReadStreamObservation,
    [switch]$ApproveScannerImageReadShortRetryObservation,
    [switch]$ApproveScannerNaturalPreviewCompletion,
    [switch]$ApproveScannerNaturalPreviewPerRowRetry,
    [switch]$ApproveScannerPoweredNaturalPreviewObservation,
    [switch]$ApproveScannerPoweredPreviewCleanupCompletion,
    [switch]$ApproveScannerPoweredPreviewCancellation,
    [switch]$ApproveScannerFullScanStartupFingerprint,
    [switch]$ApproveScannerFullScanSetWindow,
    [switch]$ApproveScannerFullScanCompletion,
    [switch]$ApproveScannerFullScanTerminalProbe,
    [switch]$ApproveScannerFullScanNaturalCompletion,
    [switch]$ApproveScannerFullScanRow997Completion,
    [switch]$ApproveScannerFullScanProgressCompletion,
    [switch]$ApproveScannerOperatorRepeatSession,
    [switch]$ApproveTrustedFlexColorPassThrough
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ([string]::IsNullOrWhiteSpace($PrivateRoot)) {
    $PrivateRoot = Join-Path $repoRoot 'out\flexcolor-aspi-private'
}
$launcher = Join-Path $repoRoot 'out\bin\flexcolor-aspi-launcher.exe'
if (-not (Test-Path -LiteralPath $launcher) -or
    -not (Test-Path -LiteralPath $PrivateRoot)) {
    & (Join-Path $repoRoot 'scripts\Prepare-FlexColorAspiTest.ps1') `
        -PrivateRoot $PrivateRoot
}

switch ($Action) {
    'Preflight' {
        $launcherArguments = @('preflight', $PrivateRoot)
    }
    'Smoke' {
        $launcherArguments = @('smoke-replay', $PrivateRoot,
            '--approve-offline-replay-smoke')
    }
    'OperationalReplaySmoke' {
        $launcherArguments = @('smoke-operational-replay', $PrivateRoot,
            '--approve-offline-operational-replay-smoke')
    }
    'FrameOperationalReplaySmoke' {
        if ([string]::IsNullOrWhiteSpace($Frame)) {
            throw 'FrameOperationalReplaySmoke requires -Frame.'
        }
        $launcherArguments = @('smoke-operational-replay-frame',
            $PrivateRoot, $Frame,
            '--approve-offline-operational-replay-smoke')
    }
    'FullScanReplaySmoke' {
        if ($Frame -ne '60x60') {
            throw 'FullScanReplaySmoke requires -Frame 60x60.'
        }
        $launcherArguments = @('smoke-offline-full-scan',
            $PrivateRoot, $Frame,
            '--approve-offline-full-scan-replay-smoke')
    }
    'OperatorRepeatReplaySmoke' {
        if ($Frame -ne '60x60') {
            throw 'OperatorRepeatReplaySmoke requires -Frame 60x60.'
        }
        $launcherArguments = @('smoke-offline-operator-repeat',
            $PrivateRoot, $Frame,
            '--approve-offline-operator-repeat-replay-smoke')
    }
    'OfflineCancelReplaySmoke' {
        if ($Frame -ne '60x60') {
            throw 'OfflineCancelReplaySmoke requires -Frame 60x60.'
        }
        $launcherArguments = @('smoke-offline-preview-cancel',
            $PrivateRoot, $Frame,
            '--approve-offline-preview-cancel-replay-smoke')
    }
    'LiveOperatorRepeat60x60Smoke' {
        if (-not $ApproveScannerOperatorRepeatSession) {
            throw 'LiveOperatorRepeat60x60Smoke requires -ApproveScannerOperatorRepeatSession.'
        }
        if ($Frame -ne '60x60') {
            throw 'LiveOperatorRepeat60x60Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @('smoke-live-operator-repeat-60x60',
            $PrivateRoot, $Frame,
            '--approve-live-operator-repeat-60x60-smoke')
    }
    'Launch' {
        $launcherArguments = @('launch-replay', $PrivateRoot,
            '--approve-offline-replay')
    }
    'LiveSmoke' {
        $launcherArguments = @('smoke-live-read-only', $PrivateRoot,
            '--approve-live-read-only-smoke')
    }
    'LiveLaunch' {
        $launcherArguments = @('launch-live-read-only', $PrivateRoot,
            '--approve-live-read-only')
    }
    'TrustedPassThroughLaunch' {
        if (-not $ApproveTrustedFlexColorPassThrough) {
            throw (
                'TrustedPassThroughLaunch requires ' +
                '-ApproveTrustedFlexColorPassThrough.')
        }
        $launcherArguments = @(
            'launch-trusted-flexcolor-pass-through', $PrivateRoot,
            '--approve-trusted-flexcolor-pass-through')
    }
    'LoaderPreflight' {
        $launcherArguments = @('loader-preflight', $PrivateRoot)
    }
    'LoaderLaunch' {
        if (-not $ApproveScannerFirmwareStateChange) {
            throw 'LoaderLaunch requires -ApproveScannerFirmwareStateChange.'
        }
        $launcherArguments = @('launch-live-loader', $PrivateRoot,
            '--approve-scanner-firmware-state-change')
    }
    'PreviewObserveLaunch' {
        if (-not $ApproveScannerPreviewObservation) {
            throw 'PreviewObserveLaunch requires -ApproveScannerPreviewObservation.'
        }
        $launcherArguments = @('launch-live-preview-observe', $PrivateRoot,
            '--approve-one-preview-set-window-observation')
    }
    'PreviewFingerprintSmoke' {
        if (-not $ApproveScannerPreviewFingerprintObservation) {
            throw 'PreviewFingerprintSmoke requires -ApproveScannerPreviewFingerprintObservation.'
        }
        if ([string]::IsNullOrWhiteSpace($Frame)) {
            throw 'PreviewFingerprintSmoke requires -Frame.'
        }
        $launcherArguments = @('smoke-live-preview-fingerprint', $PrivateRoot,
            $Frame,
            '--approve-preview-fingerprint-only-smoke')
    }
    'PreviewObserveSmoke' {
        if (-not $ApproveScannerPreviewObservation) {
            throw 'PreviewObserveSmoke requires -ApproveScannerPreviewObservation.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewObserveSmoke requires -Frame 60x60.'
        }
        $launcherArguments = @('smoke-live-preview-observe', $PrivateRoot,
            $Frame,
            '--approve-bounded-preview-observation-smoke')
    }
    'Preview24x36SetWindowObserveSmoke' {
        if (-not $ApproveScanner24x36PreviewSetWindowObservation) {
            throw 'Preview24x36SetWindowObserveSmoke requires -ApproveScanner24x36PreviewSetWindowObservation.'
        }
        if ($Frame -ne '24x36') {
            throw 'Preview24x36SetWindowObserveSmoke requires -Frame 24x36.'
        }
        $launcherArguments = @(
            'smoke-live-preview-set-window-24x36-observe',
            $PrivateRoot, $Frame,
            '--approve-bounded-24x36-preview-set-window-observation-smoke')
    }
    'Preview24x36FirstReadSmoke' {
        if (-not $ApproveScanner24x36FirstImageReadObservation) {
            throw 'Preview24x36FirstReadSmoke requires -ApproveScanner24x36FirstImageReadObservation.'
        }
        if ($Frame -ne '24x36') {
            throw 'Preview24x36FirstReadSmoke requires -Frame 24x36.'
        }
        $launcherArguments = @('smoke-live-preview-first-read-24x36',
            $PrivateRoot, $Frame,
            '--approve-one-24x36-preview-first-image-read-smoke')
    }
    'Preview4x5FirstReadSmoke' {
        if (-not $ApproveScanner4x5FirstImageReadObservation) {
            throw 'Preview4x5FirstReadSmoke requires -ApproveScanner4x5FirstImageReadObservation.'
        }
        if ($Frame -ne '4x5') {
            throw 'Preview4x5FirstReadSmoke requires -Frame 4x5.'
        }
        $launcherArguments = @('smoke-live-preview-first-read-4x5',
            $PrivateRoot, $Frame,
            '--approve-one-4x5-preview-first-image-read-smoke')
    }
    'PreviewFirstReadSmoke' {
        if (-not $ApproveScannerFirstImageReadObservation) {
            throw 'PreviewFirstReadSmoke requires -ApproveScannerFirstImageReadObservation.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewFirstReadSmoke requires -Frame 60x60.'
        }
        $launcherArguments = @('smoke-live-preview-first-read',
            $PrivateRoot, $Frame,
            '--approve-one-preview-first-image-read-smoke')
    }
    'PreviewBurst8Smoke' {
        if (-not $ApproveScannerImageReadBurstObservation) {
            throw 'PreviewBurst8Smoke requires -ApproveScannerImageReadBurstObservation.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewBurst8Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @('smoke-live-preview-burst-8',
            $PrivateRoot, $Frame,
            '--approve-eight-preview-image-read-burst-smoke')
    }
    'PreviewStream256Smoke' {
        if (-not $ApproveScannerImageReadStreamObservation) {
            throw 'PreviewStream256Smoke requires -ApproveScannerImageReadStreamObservation.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewStream256Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @('smoke-live-preview-stream-256',
            $PrivateRoot, $Frame,
            '--approve-256-preview-image-read-stream-smoke')
    }
    'PreviewStream256ShortRetrySmoke' {
        if (-not $ApproveScannerImageReadShortRetryObservation) {
            throw 'PreviewStream256ShortRetrySmoke requires -ApproveScannerImageReadShortRetryObservation.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewStream256ShortRetrySmoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-preview-stream-256-short-retry',
            $PrivateRoot, $Frame,
            '--approve-256-preview-image-read-short-retry-smoke')
    }
    'PreviewNatural996ShortRetrySmoke' {
        if (-not $ApproveScannerNaturalPreviewCompletion) {
            throw 'PreviewNatural996ShortRetrySmoke requires -ApproveScannerNaturalPreviewCompletion.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewNatural996ShortRetrySmoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-preview-natural-996-short-retry',
            $PrivateRoot, $Frame,
            '--approve-natural-996-preview-image-read-smoke')
    }
    'PreviewNatural996PerRow8Smoke' {
        if (-not $ApproveScannerNaturalPreviewPerRowRetry) {
            throw 'PreviewNatural996PerRow8Smoke requires -ApproveScannerNaturalPreviewPerRowRetry.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewNatural996PerRow8Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-preview-natural-996-per-row-8',
            $PrivateRoot, $Frame,
            '--approve-natural-996-per-row-preview-image-read-smoke')
    }
    'PreviewPoweredNatural911ObserveSmoke' {
        if (-not $ApproveScannerPoweredNaturalPreviewObservation) {
            throw 'PreviewPoweredNatural911ObserveSmoke requires -ApproveScannerPoweredNaturalPreviewObservation.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewPoweredNatural911ObserveSmoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-preview-powered-natural-911-observe',
            $PrivateRoot, $Frame,
            '--approve-powered-natural-911-preview-observation-smoke')
    }
    'PreviewPoweredNatural911CleanupSmoke' {
        if (-not $ApproveScannerPoweredPreviewCleanupCompletion) {
            throw 'PreviewPoweredNatural911CleanupSmoke requires -ApproveScannerPoweredPreviewCleanupCompletion.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewPoweredNatural911CleanupSmoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-preview-powered-natural-911-cleanup-2',
            $PrivateRoot, $Frame,
            '--approve-powered-natural-911-cleanup-2-smoke')
    }
    'PreviewPoweredCancel32To96CleanupSmoke' {
        if (-not $ApproveScannerPoweredPreviewCancellation) {
            throw 'PreviewPoweredCancel32To96CleanupSmoke requires -ApproveScannerPoweredPreviewCancellation.'
        }
        if ($Frame -ne '60x60') {
            throw 'PreviewPoweredCancel32To96CleanupSmoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-preview-powered-cancel-32-96-cleanup-2',
            $PrivateRoot, $Frame,
            '--approve-powered-preview-cancel-32-96-cleanup-2-smoke')
    }
    'FullScanStartupFingerprintSmoke' {
        if (-not $ApproveScannerFullScanStartupFingerprint) {
            throw 'FullScanStartupFingerprintSmoke requires -ApproveScannerFullScanStartupFingerprint.'
        }
        if ($Frame -ne '60x60') {
            throw 'FullScanStartupFingerprintSmoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-full-scan-startup-fingerprint',
            $PrivateRoot, $Frame,
            '--approve-full-scan-startup-fingerprint-smoke')
    }
    'FullScanSetWindowFirstReadSmoke' {
        if (-not $ApproveScannerFullScanSetWindow) {
            throw 'FullScanSetWindowFirstReadSmoke requires -ApproveScannerFullScanSetWindow.'
        }
        if ($Frame -ne '60x60') {
            throw 'FullScanSetWindowFirstReadSmoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-full-scan-set-window-first-read',
            $PrivateRoot, $Frame,
            '--approve-full-scan-set-window-first-read-smoke')
    }
    'FullScanComplete60x60Smoke' {
        if (-not $ApproveScannerFullScanCompletion) {
            throw 'FullScanComplete60x60Smoke requires -ApproveScannerFullScanCompletion.'
        }
        if ($Frame -ne '60x60') {
            throw 'FullScanComplete60x60Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-full-scan-complete-60x60',
            $PrivateRoot, $Frame,
            '--approve-full-scan-complete-60x60-smoke')
    }
    'FullScanTerminalProbe60x60Smoke' {
        if (-not $ApproveScannerFullScanTerminalProbe) {
            throw 'FullScanTerminalProbe60x60Smoke requires -ApproveScannerFullScanTerminalProbe.'
        }
        if ($Frame -ne '60x60') {
            throw 'FullScanTerminalProbe60x60Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-full-scan-terminal-probe-60x60',
            $PrivateRoot, $Frame,
            '--approve-full-scan-terminal-probe-60x60-smoke')
    }
    'FullScanNatural996Complete60x60Smoke' {
        if (-not $ApproveScannerFullScanNaturalCompletion) {
            throw 'FullScanNatural996Complete60x60Smoke requires -ApproveScannerFullScanNaturalCompletion.'
        }
        if ($Frame -ne '60x60') {
            throw 'FullScanNatural996Complete60x60Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-full-scan-natural-996-complete-60x60',
            $PrivateRoot, $Frame,
            '--approve-full-scan-natural-996-complete-60x60-smoke')
    }
    'FullScanRow997Complete60x60Smoke' {
        if (-not $ApproveScannerFullScanRow997Completion) {
            throw 'FullScanRow997Complete60x60Smoke requires -ApproveScannerFullScanRow997Completion.'
        }
        if ($Frame -ne '60x60') {
            throw 'FullScanRow997Complete60x60Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-full-scan-row997-complete-60x60',
            $PrivateRoot, $Frame,
            '--approve-full-scan-row997-complete-60x60-smoke')
    }
    'FullScanProgressComplete60x60Smoke' {
        if (-not $ApproveScannerFullScanProgressCompletion) {
            throw 'FullScanProgressComplete60x60Smoke requires -ApproveScannerFullScanProgressCompletion.'
        }
        if ($Frame -ne '60x60') {
            throw 'FullScanProgressComplete60x60Smoke requires -Frame 60x60.'
        }
        $launcherArguments = @(
            'smoke-live-full-scan-progress-complete-60x60',
            $PrivateRoot, $Frame,
            '--approve-full-scan-progress-complete-60x60-smoke')
    }
}

# Some automation hosts supply both Path and PATH in the same Windows
# environment block. .NET Framework ProcessStartInfo treats those names as
# equal and otherwise refuses to create FlexColor's child process. PowerShell
# 7.4+ can replace both entries in the launcher's environment with one PATH.
$startProcess = Get-Command Start-Process
if ($startProcess.Parameters.ContainsKey('Environment')) {
    $seenPathEntries = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $normalizedPathEntries = [Collections.Generic.List[string]]::new()
    $environment = [Environment]::GetEnvironmentVariables()
    foreach ($key in $environment.Keys) {
        if (-not [string]::Equals([string]$key, 'Path',
                [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }
        foreach ($entry in ([string]$environment[$key]).Split(';')) {
            if (-not [string]::IsNullOrWhiteSpace($entry) -and
                $seenPathEntries.Add($entry)) {
                $normalizedPathEntries.Add($entry)
            }
        }
    }
    $normalizedPath = [string]::Join(';', $normalizedPathEntries)

    # Start-Process joins ArgumentList into a native command line. Windows
    # paths cannot contain a quote; trim a trailing separator before quoting
    # so a custom private root containing spaces remains one argument.
    $nativeArguments = foreach ($argument in $launcherArguments) {
        if ($argument.Contains('"')) {
            throw 'Launcher arguments must not contain a quotation mark.'
        }
        '"' + $argument.TrimEnd('\') + '"'
    }
    $process = Start-Process -FilePath $launcher `
        -ArgumentList $nativeArguments -WorkingDirectory $repoRoot `
        -Environment @{ 'PATH' = $normalizedPath } -NoNewWindow `
        -Wait -PassThru
    exit $process.ExitCode
}

# Windows PowerShell 5.1 has no -Environment option. Normal interactive
# shells normally contain a single case-insensitive Path entry, so preserve
# compatibility and let the launcher's fail-closed error report any anomaly.
& $launcher $launcherArguments
exit $LASTEXITCODE
