# fcusb — USB2Xchange for FlexColor

> **EXPERIMENTAL SOFTWARE — USE AT YOUR OWN RISK.** This unofficial, unfinished
> proof of concept communicates with legacy scanner hardware and loads firmware
> into the USB2Xchange adapter and scanner. A defect, unsupported configuration,
> interrupted operation, or incorrect procedure could cause failed scans, data
> loss, device malfunction, loss of scanner access, or hardware damage. There is
> no warranty and no guarantee of recovery or individual support. Do not use
> valuable originals or production equipment unless you understand and accept
> these risks. Read [the full disclaimer](DISCLAIMER.md).

A working, experimental user-mode bridge for **English FlexColor 4.0.3** and a
**FlexTight Precision II at SCSI target 5 / LUN 0**, through the **Adaptec
USB2Xchange (VID 03F3, loader PID 2002, operational PID 2003)** on **Windows 10
x64 and Windows 11 x64**. It is for advanced users and developers, manually
installed and configured, and not production-ready. It is not an official
Hasselblad, Imacon, Adaptec, Microsoft, or successor-vendor product.

The project uses Microsoft's signed inbox WinUSB driver. **No project kernel
driver is needed in the normal path. Do not install the legacy XP driver.**
The exact original firmware lacks `USB\MS_COMP_WINUSB`, so first setup requires
manual WinUSB selection once for PID 2002 and once for PID 2003. The manager
registers the interface and handles later firmware initialization.

## Start here

**[Download v0.1.0-poc.1](https://github.com/aidanc/fcusb/releases/tag/v0.1.0-poc.1)**
— unsigned experimental setup EXE, runtime ZIP, exact source ZIP and SHA-256 manifests.

1. Follow the **[illustrated user guide](docs/USER_GUIDE.md)**. No compiler is
   needed for the downloadable runtime.
2. Want to explore without a scanner? Extract the runtime ZIP and double-click
   **Try demo.cmd**, or choose **Try demo** in the manager. The same setup and
   adapter screens run entirely in memory with a prominent demo banner.
3. For real operation, read [Installation](docs/INSTALLATION.md),
   [required external files and hashes](docs/REQUIRED_EXTERNAL_FILES.md) and
   [limitations](docs/KNOWN_LIMITATIONS.md). Supply your own licensed inputs.
4. Run setup, prepare the private application copy, acknowledge the first-use
   hardware warning, and configure [both WinUSB identities](docs/USB2XCHANGE_WINUSB_SETUP.md).
5. Use **FlexColor with USB2Xchange** for [daily operation](docs/DAILY_USE.md).
   Developers can still [build from source](docs/BUILDING.md).

![The actual manager running its hardware-free demo](docs/images/05-session.png)

The download includes an offline illustrated guide: choose **User guide** in
the manager or open `docs/USER_GUIDE.html`. Demo mode rehearses the manager
workflow; it does not emulate the FlexColor scan interface or validate hardware.

The script-driven portable runtime has recorded clean Windows 10 and 11
Preview/full Scan/save/reconnect acceptance. The newer setup/manager/shortcut,
repair, acknowledgement, and uninstall experience has offline coverage but
**still requires full clean-VM GUI qualification**. Powered `60x70`, in-flight
unplug/stall, and authentic useful CHECK CONDITION/sense remain unqualified.
Unsupported scanners, adapters, FlexColor versions and Windows configurations
are not guaranteed to work.

## Documentation

- [Windows prerequisites](docs/WINDOWS_10_11_SETUP.md), [FlexColor setup](docs/FLEXCOLOR_SETUP.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md), [uninstall and rollback](docs/UNINSTALL.md)
- [Architecture](docs/ARCHITECTURE.md), [protocol and evidence](docs/PROTOCOL.md)
- [Acceptance checklist](docs/FRESH_VM_ACCEPTANCE.md), [release notes](docs/RELEASE_NOTES.md)
- [Precision II sharpening](docs/PRECISION_II_SHARPENING_ANALYSIS.md),
  [3F/FFF structure](docs/3F_FILE_STRUCTURE.md), [stock M333 RAM analysis](docs/PRECISION_II_RAM_UPGRADE_ANALYSIS.md)
- [Contributing and support policy](CONTRIBUTING.md), [security](SECURITY.md),
  [publication audit](docs/PUBLICATION_AUDIT.md)

Read the complete installation and troubleshooting guides before opening an
[issue](https://github.com/aidanc/fcusb/issues). Individual installation support
and recovery assistance are not guaranteed. Never attach proprietary files,
firmware, personal scans, or unrelated logs.

## License and interoperability

Project-owned source, scripts and documentation: **GNU GPL version 3 only**,
`SPDX-License-Identifier: GPL-3.0-only`. See [LICENSE](LICENSE) and
[NOTICE](NOTICE.md). Copyright © 2026 fcusb contributors.

The GPL does not license FlexColor, firmware, Microsoft runtime files, manuals,
or other separately obtained proprietary inputs. The compatibility DLL loads
inside the proprietary process through its existing ASPI interface; that
in-process boundary is documented in NOTICE and is not a claim that proprietary
software can be redistributed under the GPL. Source and build scripts are
provided; do not distribute combined proprietary application trees.
