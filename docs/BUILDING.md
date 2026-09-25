<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Building

## Verified environment

The complete source/test build was verified on Windows 10 x64 build 19045 with:

- Visual Studio Build Tools 2022 17.14.37;
- MSVC 19.44.35228, tool directory 14.44.35207;
- Windows SDK 10.1.26100.7705;
- Windows Driver Kit 10.1.26100.6584; and
- .NET Framework 4.8.

The SDK and WDK share build number 26100. Microsoft requires matching build
numbers; the servicing/QFE suffix may differ unless a driver uses content added
only by a later QFE. See Microsoft's
[WDK installation guidance](https://learn.microsoft.com/en-us/windows-hardware/drivers/download-the-wdk).

## Minimal toolchain installation

These commands install command-line build tools, not the Visual Studio IDE:

```powershell
winget install --exact --id Microsoft.VisualStudio.2022.BuildTools `
  --version 17.14.37 --source winget `
  --accept-source-agreements --accept-package-agreements `
  --override "--wait --passive --norestart `
    --add Microsoft.Component.MSBuild `
    --add Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64 `
    --add Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64.Spectre `
    --add Microsoft.VisualStudio.Component.Windows11SDK.26100 `
    --add Component.Microsoft.Windows.DriverKit.BuildTools"

winget install --exact --id Microsoft.WindowsWDK.10.0.26100 `
  --version 10.1.26100.6584 --source winget `
  --accept-source-agreements --accept-package-agreements `
  --silent --disable-interactivity
```

Neither command enables test signing, creates a certificate, installs a project
driver, or changes a device association.

## Build and test

Run from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

The script:

1. parses every driver-support PowerShell script and tests its pure helper
   functions without elevation or system changes;
2. builds the x64 WinUSB diagnostic and existing protocol tests;
3. builds the reversible FlexColor patch utility and its generated-fixture
   tests;
4. builds the x86 ASPI diagnostic and its tests;
5. builds the managed miniport/broker protocol model, checkpoint console, and
   tests;
6. compiles native C ABI assertions with MSVC `/W4 /WX`;
7. builds the AMD64 Storport virtual miniport with the WDK, `/W4 /WX`, SDL
   checks, and Spectre mitigation;
8. stages the `.sys` plus source INF under `out\driver\package` and runs
   Inf2Cat signability validation for Windows 10 x64; and
9. runs the broker's device-free offline protocol dry-run.

All generated objects and binaries are written under ignored `out\` paths.
The miniport binary is `out\driver\Release\usb2xchange-vminiport.sys`. Its
package is intentionally unsigned; both the `.sys` and generated `.cat` report
`NotSigned`. Building and running the tests does not install a device, create a
service, load a driver, create a certificate, or change Windows boot policy.

Do not attempt to install the package from `out\driver\package`. Installation
is a later, explicit hardware-test step requiring a fresh VM checkpoint and a
test-signed package.

The patch utility can be built without MSVC or the WDK:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Build-FlexColorAspiPatch.ps1
```

It is written to ignored `out\bin\flexcolor-aspi-patch.exe`. See
[FLEXCOLOR_SETUP](FLEXCOLOR_SETUP.md) before using it; the utility refuses
the installed FlexColor tree and does not launch the application or access
hardware.

## Public source and package commands

The full suite remains device-free, including the research kernel build. It
needs the documented SDK/WDK even though normal use needs no project kernel
driver. Building does not authorize driver installation.

From a clean public commit:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-Usb2XchangeSourceRelease.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-Usb2XchangeBinaryRelease.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-Usb2XchangeBinaryRelease.ps1 -AuditOnly
```

Outputs are under ignored out/release, named with the exact public commit.
Packaging reruns tests, stages the explicit project-only allowlist, embeds that
archive in setup, and runs bootstrap/manager dependency self-tests. No proprietary
inputs are needed for the normal suite. Optional licensed-input package smokes
perform no scanner operation. Retain exact source, LICENSE and notices with
conveyed binaries. Byte-identical PE/ZIP output is not promised. Tags/releases
require owner approval.
