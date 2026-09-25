# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$compiler = Join-Path $env:WINDIR `
    'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The required .NET Framework C# compiler was not found at $compiler"
}

$outputDirectory = Join-Path $repoRoot 'out\bin'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$output = Join-Path $outputDirectory 'flexcolor-aspi-patch.exe'
$sources = Get-ChildItem -LiteralPath (
    Join-Path $repoRoot 'src\Usb2Xchange.FlexColorPatch') -Filter '*.cs' |
    Select-Object -ExpandProperty FullName

& $compiler /nologo /checked+ /debug:pdbonly /optimize+ /platform:x64 `
    /target:exe "/out:$output" $sources
if ($LASTEXITCODE -ne 0) {
    throw 'FlexColor patch utility compilation failed.'
}

Write-Output "Built $output"
