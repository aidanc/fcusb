<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Required external files and legitimate acquisition

Checked 2026-09-25. The repository and project packages contain **none** of
these proprietary inputs. The GPL does not cover them. Obtain them under your
own applicable license; the project does not grant redistribution rights.

## Acquisition routes

| Input / publisher | Why needed and legitimate route | Rights / verification status |
|---|---|---|
| English FlexColor 4.0.3 / Imacon-Hasselblad | Complete scanner application. Start with [Hasselblad downloads](https://www.hasselblad.com/downloads/) or [support](https://www.hasselblad.com/support/). The [authorized repair workshop's 4.0.3 PC listing](https://hasselbladrepair.com/webshop/software/flexcolor-4-0-3-windows/) is a verified product/acquisition page, not a verified direct binary. Otherwise copy your complete licensed installation/original media. | Proprietary/restricted. Listing verified; downloaded installer payload, language and hashes not independently verified. Do not assume the listing supplies the exact supported tree. |
| `usb2xchange.fw` / Adaptec | Volatile adapter runtime image. No authorized current public direct download was verified. Use your own original USB2Xchange media/expanded `Adpusbld.sys` and the hash-locked extractor below, or an already lawfully held exact image. [Adaptec support](https://ask.adaptec.com/app/answers/list/p/686/kw/driver) is a vendor support landing/search page, not a firmware download. | Redistribution unclear; excluded. Historical research archives were evidence, not an established redistribution license; no firmware mirror is recommended here. |
| `Firmware\MICROCOD.3XX` / Imacon-Hasselblad | Scanner CPU image for L302→M333, supplied within the licensed complete FlexColor tree. Use the same vendor/workshop route above or original media. | Proprietary/restricted; no separate authorized public exact-image download verified. |
| x86 `MFC71.dll`, `MSVCR71.dll`, `MSVCP71.dll` / Microsoft | FlexColor's VC++ 7.1 dependencies. Copy the exact versions from your licensed installation or its existing Windows `%WINDIR%\SysWOW64` when present; original licensed installation media is the recovery source. [Microsoft developer documentation](https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files?view=msvc-170) is background, not a VC++ 7.1 download. | Microsoft proprietary terms apply; project redistribution rights not established. No authorized standalone download of these exact hashes verified. New runtime packages and DLL-download sites are not substitutes. |

Do not execute an Adaptec installer/driver to inspect it. Extract your own
original archive with an archive tool, or copy the already expanded driver from
your media. Never install the legacy XP kernel driver on Windows 10/11.

For the exact 27,472-byte `Adpusbld.sys` version 2.0.0.3 with SHA-256
`FA2629BDF855B5A320D2C184B40FFB2B780D8FDB67491504CEF2C5AA0E3E8381`:

```powershell
New-Item -ItemType Directory -Path C:\local-inputs -Force
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Extract-Usb2XchangeFirmware.ps1 -LegacyLoaderPath 'D:\original-media\Adpusbld.sys' -OutputPath 'C:\local-inputs\usb2xchange.fw'
Get-FileHash -Algorithm SHA256 C:\local-inputs\usb2xchange.fw
```

The source-only extractor reads a fixed record table and checks both input and
output hashes. It executes no vendor code and accesses no USB device. Its output
is a private input, not a redistributable project artifact. Unknown versions
are rejected, and existing output is never overwritten. If you lack original
media or a lawful exact copy, this prerequisite remains unavailable; do not
work around that by downloading an unauthorized mirror.

## Exact supported identities

| File | Version / bytes | SHA-256 |
|---|---|---|
| `FlexColor.exe` | English 4.0.3 / 159,744 | `4C5D402F3668F06BEAFC871B9F152D55BF5ACCA191C43082C6A8C5647916AF28` |
| `DLLS\FlexColor.dll` original | English 4.0.3 / 7,602,176 | `B49217BA2BBFF2E9A9DF0952CC9657CD197C10022E2B62E3B818719FB78C1E84` |
| `usb2xchange.fw` | 588 records plus terminator / 16,492 | `D0967EF81E71E9293D0499C91D687E2409F8CA13B14FFE2C4F35F07685D25FBD` |
| `Firmware\MICROCOD.3XX` | M333 / 65,536 | `D8D7188574C52B255CF7940BEF7CC9192F744693AABC1F735758B7FECDEC65F3` |
| `MFC71.dll` | x86 7.10.3077.0 / 1,060,864 | `4DA5EFDC46D126B45DAEEE8BC69C0BA2AA243589046B7DFD12A7E21B9BEE6A32` |
| `MSVCR71.dll` | x86 7.10.3052.4 / 348,160 | `8094AF5EE310714CAEBCCAEEE7769FFB08048503BA478B879EDFEF5F1A24FEFE` |
| `MSVCP71.dll` | x86 7.10.3077.0 / 499,712 | `DF96156F6A548FD6FE5672918DE5AE4509D3C810A57BFFD2A91DE45A3ED5B23B` |

Example verification, substituting your licensed input paths:

```powershell
$fcInput = 'C:\licensed\FlexColor403'
Get-Item "$fcInput\FlexColor.exe","$fcInput\DLLS\FlexColor.dll","$fcInput\Firmware\MICROCOD.3XX" | Select-Object Name,Length
Get-FileHash -Algorithm SHA256 "$fcInput\FlexColor.exe","$fcInput\DLLS\FlexColor.dll","$fcInput\Firmware\MICROCOD.3XX"
Get-FileHash -Algorithm SHA256 C:\licensed\vc71\MFC71.dll,C:\licensed\vc71\MSVCR71.dll,C:\licensed\vc71\MSVCP71.dll
```

Preserve the entire FlexColor tree, not just these files. Select that tree in
Setup. Keep scanner firmware in its original Firmware subfolder. Select the
adapter file separately; setup stores a local copy in
`%LOCALAPPDATA%\USB2Xchange\UserData`. Runtime DLLs may be in the source root,
SysWOW64, or an explicit runtime directory; setup deploys them application-locally.

For missing files, return to original media/licensed installation or the vendor.
For wrong hashes, check language/version, x86 architecture, truncation and prior
patches; restore a clean original. Do not rename an unrelated DLL, replace system
files, change hash constants, or upload the failing binary to an issue.

## Manuals and Windows prerequisites

- [Hasselblad FlexColor scanner manual](https://cdn.hasselblad.com/0b68568e-5c54-4989-a1b6-a38f46177406_flexcolor-manual-scanners.pdf): direct vendor-hosted PDF; application reference.
- [Precision II manual](https://www.hasselbladrepair.com/wp-content/uploads/2016/12/FlextightPrecision2-Manual.pdf): direct Imacon manual PDF from the authorized repair workshop; [manuals landing page](https://hasselbladrepair.com/service-2/manuals/). Follow its physical safety instructions. It describes historical SCSI software; use this project's modern binding instructions.
- [Microsoft WinUSB installation](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/winusb-installation) and [compatible-ID mechanism](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/automatic-installation-of-winusb): documentation, not replacement-driver downloads.
- [Microsoft .NET Framework setup](https://learn.microsoft.com/en-us/dotnet/framework/install/guide-for-developers): official prerequisite route. [BUILDING](BUILDING.md) covers SDK/WDK/build tools.

Manuals remain copyrighted by their publishers; permission to redistribute was
not established, so none is mirrored. The historical Microsemi USB support URL
could not be verified; no Internet Archive or anonymous mirror is substituted.
See [external URL checks](EXTERNAL_URL_REVIEW.md) for publication-time results.
