# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [string]$OutputDirectory,
    [switch]$AuditOnly,
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))).
    TrimEnd('\')
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'out')).TrimEnd('\')
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $outputRoot 'release'
}
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if (-not $output.StartsWith($outputRoot + '\',
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must remain under the ignored output tree: $outputRoot"
}

$git = (Get-Command git -ErrorAction Stop).Source
$actualRoot = (& $git -C $repoRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or
    [IO.Path]::GetFullPath($actualRoot).TrimEnd('\') -ne $repoRoot) {
    throw 'The script is not running from the expected Git worktree.'
}
if (-not $AllowDirty) {
    $changes = @(& $git -C $repoRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or $changes.Count -ne 0) {
        throw 'Refusing to stage a binary release from a dirty worktree.'
    }
}

$commit = (& $git -C $repoRoot rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve the release commit.'
}

$files = @(
    @{ Source = 'out/bin/usb2xchange-manager.exe'; Path = 'USB2Xchange.exe' },
    @{ Source = 'out/bin/usb2xchange.exe'; Path = 'out/bin/usb2xchange.exe' },
    @{ Source = 'out/bin/Usb2Xchange.WinUsb.dll'; Path = 'out/bin/Usb2Xchange.WinUsb.dll' },
    @{ Source = 'out/bin/Usb2Xchange.Protocol.dll'; Path = 'out/bin/Usb2Xchange.Protocol.dll' },
    @{ Source = 'out/bin/flexcolor-aspi-patch.exe'; Path = 'out/bin/flexcolor-aspi-patch.exe' },
    @{ Source = 'out/bin/flexcolor-aspi-launcher.exe'; Path = 'out/bin/flexcolor-aspi-launcher.exe' },
    @{ Source = 'out/aspi/wnaspi32.dll'; Path = 'out/aspi/wnaspi32.dll' },
    @{ Source = 'out/aspi/wnaspi32.dll.config'; Path = 'out/aspi/wnaspi32.dll.config' },
    @{ Source = 'out/aspi/Usb2Xchange.AspiShim.Managed.dll'; Path = 'out/aspi/Usb2Xchange.AspiShim.Managed.dll' },
    @{ Source = 'out/aspi/Usb2Xchange.WinUsb.dll'; Path = 'out/aspi/Usb2Xchange.WinUsb.dll' },
    @{ Source = 'out/aspi/Usb2Xchange.Protocol.dll'; Path = 'out/aspi/Usb2Xchange.Protocol.dll' },
    @{ Source = 'out/aspi/FlexColor.exe.config'; Path = 'out/aspi/FlexColor.exe.config' },
    @{ Source = 'out/aspi/Usb2Xchange.AspiProbe.exe'; Path = 'out/aspi/Usb2Xchange.AspiProbe.exe' },
    @{ Source = 'scripts/Prepare-FlexColorAspiTest.ps1'; Path = 'scripts/Prepare-FlexColorAspiTest.ps1' },
    @{ Source = 'scripts/Usb2Xchange-FlexColor.ps1'; Path = 'scripts/Usb2Xchange-FlexColor.ps1' },
    @{ Source = 'scripts/Usb2Xchange-EndUser.ps1'; Path = 'scripts/Usb2Xchange-EndUser.ps1' },
    @{ Source = 'driver/Set-Usb2XchangeInterfaceGuid.ps1'; Path = 'driver/Set-Usb2XchangeInterfaceGuid.ps1' },
    @{ Source = 'docs/OPERATOR_WORKFLOW.md'; Path = 'docs/OPERATOR_WORKFLOW.md' },
    @{ Source = 'docs/FRESH_VM_ACCEPTANCE.md'; Path = 'docs/FRESH_VM_ACCEPTANCE.md' },
    @{ Source = 'docs/END_USER_PACKAGE.md'; Path = 'docs/END_USER_PACKAGE.md' },
    @{ Source = 'README.md'; Path = 'README.md' },
    @{ Source = 'LICENSE'; Path = 'LICENSE' },
    @{ Source = 'NOTICE.md'; Path = 'NOTICE.md' },
    @{ Source = 'DISCLAIMER.md'; Path = 'DISCLAIMER.md' },
    @{ Source = 'docs/INSTALLATION.md'; Path = 'docs/INSTALLATION.md' },
    @{ Source = 'docs/REQUIRED_EXTERNAL_FILES.md'; Path = 'docs/REQUIRED_EXTERNAL_FILES.md' },
    @{ Source = 'docs/WINDOWS_10_11_SETUP.md'; Path = 'docs/WINDOWS_10_11_SETUP.md' },
    @{ Source = 'docs/USB2XCHANGE_WINUSB_SETUP.md'; Path = 'docs/USB2XCHANGE_WINUSB_SETUP.md' },
    @{ Source = 'docs/FLEXCOLOR_SETUP.md'; Path = 'docs/FLEXCOLOR_SETUP.md' },
    @{ Source = 'docs/DAILY_USE.md'; Path = 'docs/DAILY_USE.md' },
    @{ Source = 'docs/TROUBLESHOOTING.md'; Path = 'docs/TROUBLESHOOTING.md' },
    @{ Source = 'docs/KNOWN_LIMITATIONS.md'; Path = 'docs/KNOWN_LIMITATIONS.md' },
    @{ Source = 'docs/UNINSTALL.md'; Path = 'docs/UNINSTALL.md' },
    @{ Source = 'docs/RELEASE_NOTES.md'; Path = 'docs/RELEASE_NOTES.md' },
    @{ Source = 'CONTRIBUTING.md'; Path = 'CONTRIBUTING.md' },
    @{ Source = 'SECURITY.md'; Path = 'SECURITY.md' },
    @{ Source = 'docs/ARCHITECTURE.md'; Path = 'docs/ARCHITECTURE.md' },
    @{ Source = 'docs/BUILDING.md'; Path = 'docs/BUILDING.md' },
    @{ Source = 'docs/PROTOCOL.md'; Path = 'docs/PROTOCOL.md' },
    @{ Source = 'docs/REVERSE_ENGINEERING.md'; Path = 'docs/REVERSE_ENGINEERING.md' },
    @{ Source = 'docs/3F_FILE_STRUCTURE.md'; Path = 'docs/3F_FILE_STRUCTURE.md' },
    @{ Source = 'docs/PRECISION_II_SHARPENING_ANALYSIS.md'; Path = 'docs/PRECISION_II_SHARPENING_ANALYSIS.md' },
    @{ Source = 'docs/PRECISION_II_RAM_UPGRADE_ANALYSIS.md'; Path = 'docs/PRECISION_II_RAM_UPGRADE_ANALYSIS.md' },
    @{ Source = 'docs/EXTERNAL_URL_REVIEW.md'; Path = 'docs/EXTERNAL_URL_REVIEW.md' },
    @{ Source = 'docs/PUBLICATION_AUDIT.md'; Path = 'docs/PUBLICATION_AUDIT.md' },
    @{ Source = 'scripts/Extract-Usb2XchangeFirmware.ps1'; Path = 'scripts/Extract-Usb2XchangeFirmware.ps1' }
)
$knownProprietaryHashes = @(
    '4C5D402F3668F06BEAFC871B9F152D55BF5ACCA191C43082C6A8C5647916AF28',
    'B49217BA2BBFF2E9A9DF0952CC9657CD197C10022E2B62E3B818719FB78C1E84',
    'D250B6177D2B30FADD55E06DF612E7F534CB5D1A30E82AA9CC71A3F1924DD119',
    'D0967EF81E71E9293D0499C91D687E2409F8CA13B14FFE2C4F35F07685D25FBD',
    'D8D7188574C52B255CF7940BEF7CC9192F744693AABC1F735758B7FECDEC65F3',
    '4DA5EFDC46D126B45DAEEE8BC69C0BA2AA243589046B7DFD12A7E21B9BEE6A32',
    '8094AF5EE310714CAEBCCAEEE7769FFB08048503BA478B879EDFEF5F1A24FEFE',
    'DF96156F6A548FD6FE5672918DE5AE4509D3C810A57BFFD2A91DE45A3ED5B23B'
)
if (-not $AuditOnly) {
    & (Join-Path $repoRoot 'test.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'The complete offline suite failed before binary staging.'
    }
}

$records = New-Object Collections.Generic.List[object]
foreach ($file in $files) {
    $sourceRelative = [string]$file.Source
    $packageRelative = [string]$file.Path
    $full = [IO.Path]::GetFullPath((Join-Path $repoRoot $sourceRelative))
    if (-not $full.StartsWith($repoRoot + '\',
            [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $full -PathType Leaf) -or
        ((Get-Item -LiteralPath $full).Attributes -band
            [IO.FileAttributes]::ReparsePoint)) {
        throw "Required portable runtime file is missing or unsafe: $sourceRelative"
    }
    $item = Get-Item -LiteralPath $full
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $full).Hash
    if ($knownProprietaryHashes -contains $hash) {
        throw "Known proprietary content was selected for packaging: $sourceRelative"
    }
    $records.Add([pscustomobject][ordered]@{
            Source = $sourceRelative.Replace('\', '/')
            Path = $packageRelative.Replace('\', '/')
            Length = [int64]$item.Length
            Sha256 = $hash
        })
}

Write-Output (
    "Portable runtime audit passed: $($records.Count) allowlisted files; " +
    'no proprietary hash matched.')
Write-Output "Commit: $commit"
if ($AuditOnly) {
    Write-Output 'Audit-only mode made no files.'
    exit 0
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$shortCommit = $commit.Substring(0, 12)
$baseName = "usb2xchange-runtime-$shortCommit"
$archivePath = Join-Path $output ($baseName + '.zip')
$setupPath = Join-Path $output ("USB2Xchange-Setup-$shortCommit.exe")
$hashPath = Join-Path $output ($baseName + '.sha256.txt')
if ((Test-Path -LiteralPath $archivePath) -or
    (Test-Path -LiteralPath $setupPath) -or
    (Test-Path -LiteralPath $hashPath)) {
    throw "Refusing to overwrite an existing release artifact: $baseName"
}

$stageRoot = [IO.Path]::GetFullPath((Join-Path $output (
            '.stage-' + [Guid]::NewGuid().ToString('N'))))
if (-not $stageRoot.StartsWith($output + '\',
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The computed staging path escaped the release output directory.'
}
$packageRoot = Join-Path $stageRoot $baseName
$archiveStagePath = Join-Path $stageRoot ($baseName + '.zip')
$setupStagePath = Join-Path $stageRoot 'USB2Xchange-Setup.exe'
try {
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    foreach ($record in $records) {
        $source = Join-Path $repoRoot $record.Source
        $destination = Join-Path $packageRoot $record.Path
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) `
            -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
    $packageManifest = [pscustomobject][ordered]@{
        Schema = 1
        PackageType = 'PortableUserModeRuntime'
        Commit = $commit
        CreatedUtc = [DateTime]::UtcNow.ToString('O')
        Files = @($records | Select-Object Path, Length, Sha256)
    }
    $manifestPath = Join-Path $packageRoot `
        'USB2XCHANGE-RUNTIME-PACKAGE.json'
    $packageManifest | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $stagedDiagnostic = Join-Path $packageRoot `
        'out\bin\usb2xchange.exe'
    & $stagedDiagnostic --help | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw (
            'The staged diagnostic failed its dependency-closure smoke test: ' +
            "exit code $LASTEXITCODE")
    }

    $stagedManager = Join-Path $packageRoot 'USB2Xchange.exe'
    $managerSelfTest = Start-Process -FilePath $stagedManager `
        -ArgumentList @('--self-test') -Wait -PassThru -WindowStyle Hidden
    if ($managerSelfTest.ExitCode -ne 0) {
        throw 'The staged end-user manager failed its dependency-closure self-test.'
    }

    Compress-Archive -LiteralPath $packageRoot `
        -DestinationPath $archiveStagePath `
        -CompressionLevel Optimal

    $csc64 = Join-Path $env:WINDIR `
        'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $bootstrapSource = Join-Path $repoRoot `
        'src\Usb2Xchange.SetupBootstrap\Program.cs'
    foreach ($required in @($csc64, $bootstrapSource)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Setup bootstrap input is missing: $required"
        }
    }
    & $csc64 /nologo /checked+ /optimize+ /platform:x64 /target:winexe `
        "/out:$setupStagePath" `
        '/reference:System.Windows.Forms.dll' `
        '/reference:System.Drawing.dll' `
        '/reference:System.IO.Compression.dll' `
        '/reference:System.IO.Compression.FileSystem.dll' `
        "/resource:$archiveStagePath,Usb2Xchange.Payload.zip" `
        $bootstrapSource
    if ($LASTEXITCODE -ne 0) {
        throw 'The single-file USB2Xchange setup bootstrap failed to build.'
    }
    $setupSelfTest = Start-Process -FilePath $setupStagePath `
        -ArgumentList @('--self-test') -Wait -PassThru -WindowStyle Hidden
    if ($setupSelfTest.ExitCode -ne 0) {
        throw 'The single-file USB2Xchange setup bootstrap failed self-test.'
    }
    Move-Item -LiteralPath $archiveStagePath -Destination $archivePath
    Move-Item -LiteralPath $setupStagePath -Destination $setupPath
}
finally {
    $resolvedStage = [IO.Path]::GetFullPath($stageRoot)
    if ((Test-Path -LiteralPath $resolvedStage) -and
        $resolvedStage.StartsWith($output + '\.stage-',
            [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}

if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $setupPath -PathType Leaf)) {
    foreach ($partial in @($archivePath, $setupPath)) {
        if (Test-Path -LiteralPath $partial -PathType Leaf) {
            Remove-Item -LiteralPath $partial -Force
        }
    }
    throw 'Portable archive or single-file setup creation failed.'
}
$archiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash
$setupHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $setupPath).Hash
$hashLines = @(
    "commit=$commit",
    "package_type=PortableUserModeRuntime",
    "packaged_file_count=$($records.Count)",
    "archive_sha256=$archiveHash",
    "archive_name=$([IO.Path]::GetFileName($archivePath))",
    "setup_sha256=$setupHash",
    "setup_name=$([IO.Path]::GetFileName($setupPath))"
)
Set-Content -LiteralPath $hashPath -Value $hashLines -Encoding Ascii

Write-Output "Archive:  $archivePath"
Write-Output "SHA-256:  $archiveHash"
Write-Output "Setup:    $setupPath"
Write-Output "SHA-256:  $setupHash"
Write-Output "Manifest: $hashPath"
Write-Output 'The archive contains no FlexColor file, firmware, custom kernel driver, or certificate.'
