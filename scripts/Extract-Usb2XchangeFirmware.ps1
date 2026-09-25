# Copyright (c) 2026 fcusb contributors
# SPDX-License-Identifier: GPL-3.0-only
param(
    [Parameter(Mandatory = $true)][string]$LegacyLoaderPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$inputFile = Get-Item -LiteralPath $LegacyLoaderPath
if ($inputFile.PSIsContainer -or
    ($inputFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
    $inputFile.Length -ne 27472 -or
    (Get-FileHash -LiteralPath $inputFile.FullName -Algorithm SHA256).Hash -ne
        'FA2629BDF855B5A320D2C184B40FFB2B780D8FDB67491504CEF2C5AA0E3E8381') {
    throw 'Only the exact user-supplied Adpusbld.sys 2.0.0.3 is supported.'
}
$outputFile = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $outputFile) {
    throw 'Refusing to overwrite an existing file.'
}
$ancestor = [IO.Path]::GetDirectoryName($outputFile)
while ($ancestor) {
    if ((Test-Path -LiteralPath $ancestor) -and
        ((Get-Item -LiteralPath $ancestor).Attributes -band
            [IO.FileAttributes]::ReparsePoint)) {
        throw 'Output path contains a reparse point.'
    }
    $ancestor = [IO.Path]::GetDirectoryName($ancestor)
}
$bytes = [IO.File]::ReadAllBytes($inputFile.FullName)
$stream = New-Object IO.MemoryStream
$writer = New-Object IO.BinaryWriter($stream)
try {
    for ($index = 0; $index -lt 589; $index++) {
        $offset = 0x2FE0 + $index * 22
        $writer.Write([uint32]$bytes[$offset])
        $writer.Write([uint32][BitConverter]::ToUInt16($bytes, $offset + 2))
        $writer.Write([uint32]$bytes[$offset + 4])
        $writer.Write($bytes, $offset + 5, 16)
    }
    $writer.Flush()
    $firmware = $stream.ToArray()
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $hash = [BitConverter]::ToString($sha.ComputeHash($firmware)).Replace('-', '')
    }
    finally { $sha.Dispose() }
    if ($firmware.Length -ne 16492 -or $hash -ne
        'D0967EF81E71E9293D0499C91D687E2409F8CA13B14FFE2C4F35F07685D25FBD') {
        throw 'Extracted firmware did not match the supported image; nothing written.'
    }
    # CreateNew also refuses a file created after the initial existence check.
    $destination = [IO.File]::Open($outputFile, [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $destination.Write($firmware, 0, $firmware.Length) }
    finally { $destination.Dispose() }
    Write-Output "Extracted 16492 bytes; SHA-256 $hash"
    Write-Output 'Private local input only. No legacy code executed and no hardware accessed.'
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}
