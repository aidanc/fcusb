<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Installation

> **EXPERIMENTAL SOFTWARE — USE AT YOUR OWN RISK.** Firmware loading and scanner
> operation can cause failed scans, data loss, device malfunction or hardware
> damage. There is no warranty, guaranteed recovery or individual support.
> Protect valuable originals and production equipment. Read [DISCLAIMER](../DISCLAIMER.md).

This is a manually configured advanced-user proof of concept. Follow these
steps in order; keep the scanner off during preparation. Never install the
legacy XP Adaptec driver or change Secure Boot/signature enforcement.

1. Confirm Windows 10/11 **x64**, Windows PowerShell 5.1 and .NET Framework 4.8
   in [Windows setup](WINDOWS_10_11_SETUP.md). Obtain a VM snapshot or another
   tested rollback path before selecting a device driver. Record your build.
2. Read [external prerequisites](REQUIRED_EXTERNAL_FILES.md). Copy your complete
   licensed English FlexColor 4.0.3 tree and exact x86 VC++ 7.1 runtime to a
   local input folder. Keep its Firmware folder intact. Verify every hash.
3. Build the public source using [BUILDING](BUILDING.md). Until an owner-approved
   release exists, no GitHub setup download is implied. For a future release,
   verify the adjacent ZIP/setup SHA-256 and exact source commit before running:

   ```powershell
   Get-FileHash -Algorithm SHA256 .\USB2Xchange-Setup-<commit>.exe
   Get-FileHash -Algorithm SHA256 .\usb2xchange-runtime-<commit>.zip
   Get-AuthenticodeSignature .\USB2Xchange-Setup-<commit>.exe
   ```

   Replace `<commit>` with the actual filename suffix. Current community builds
   are unsigned (`NotSigned`); SmartScreen/unknown-publisher warnings are
   expected. A hash from an untrusted source is not proof of authenticity.
   Verify source and manifest; never globally disable Windows protections.
4. Launch the single-file setup as your normal desktop user. Alternatively,
   extract the **whole** ZIP and launch `USB2Xchange.exe` at its root. Both use
   the same payload. Setup installs under `%LOCALAPPDATA%\USB2Xchange` and does
   not need elevation for the per-user application.
5. Choose **Set up**. Select the complete licensed tree and `usb2xchange.fw`.
   Select the exact runtime folder if the DLLs are absent from the application
   root and `%WINDIR%\SysWOW64`. Setup makes a private copy, verifies it, patches
   only that copy, and creates Manager and **FlexColor with USB2Xchange** shortcuts.
6. Open **Adapter setup**. Before the first hardware-affecting action the manager
   displays the full risk warning, defaulting to No. Accept only after reading
   it. Refusal cancels. Only `risk-acknowledgement.txt` containing a warning
   version is stored under the per-user root; deleting it requires acceptance
   again. No identity/time/device details are stored or transmitted.
7. Follow [WinUSB setup](USB2XCHANGE_WINUSB_SETUP.md) once for PID 2002, initialize
   the adapter, then repeat for PID 2003. **Register interface** requires a
   narrowly scoped UAC elevation; it does not install a new kernel binary.
8. With the scanner correctly connected, terminated and set to SCSI target 5,
   power it on according to its manual. Use the project shortcut. Start should
   see exact `Imacon / SCSI Loader / L302` or `Imacon / FlexTight II / M333`.
   FlexColor normally downloads scanner microcode and reaches M333 itself.
9. Follow [daily use](DAILY_USE.md) for Preview, Scan/save and Stop. Read
   [troubleshooting](TROUBLESHOOTING.md) before retrying an unexpected result.
10. [Uninstall](UNINSTALL.md) and [clean-VM acceptance](FRESH_VM_ACCEPTANCE.md)
    describe rollback and remaining qualification. The updated GUI path is
    not yet fully qualified on clean Windows 10/11 VMs.

The ZIP contains a per-file manifest. To inspect it without execution:

```powershell
Expand-Archive .\usb2xchange-runtime-<commit>.zip C:\fcusb-package-review
Get-ChildItem C:\fcusb-package-review -Recurse -File | Select-Object FullName,Length
Get-Content C:\fcusb-package-review\usb2xchange-runtime-<commit>\USB2XCHANGE-RUNTIME-PACKAGE.json
```

Compare every entry and hash against the explicit list in
`scripts/New-Usb2XchangeBinaryRelease.ps1`. The project-built `wnaspi32.dll` is
not Adaptec ASPI. There must be no FlexColor.exe, FlexColor.dll, MICROCOD.3XX,
MFC71.dll, MSVCR71.dll, MSVCP71.dll, `.fw`, `.sys`, scan or trace in the archive.
Files ending `.exe.config`/`.dll.config` are project text.
