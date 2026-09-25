# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
param(
    [ValidateSet('Status', 'Activate', 'Deactivate')]
    [string]$Action = 'Status',

    [Parameter(Mandatory = $true)]
    [string]$FlexColorDll
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$tool = Join-Path $repoRoot 'out\bin\flexcolor-aspi-patch.exe'
if (-not (Test-Path -LiteralPath $tool)) {
    & (Join-Path $repoRoot 'scripts\Build-FlexColorAspiPatch.ps1')
}

if (-not (Test-Path -LiteralPath $FlexColorDll -PathType Leaf)) {
    throw "FlexColor DLL was not found: $FlexColorDll"
}
$resolvedDll = (Resolve-Path -LiteralPath $FlexColorDll).ProviderPath
$actionName = $Action.ToLowerInvariant()

& $tool $actionName $resolvedDll
exit $LASTEXITCODE
