<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Prepare the licensed FlexColor copy

Supply the complete **English v4.0.3** application tree, including `DLLS`,
`Firmware`, profiles, settings and other original application resources. Two
matching executables alone are insufficient. The executable is 159,744 bytes;
original DLL is 7,602,176 bytes. Verify their hashes and
`Firmware\MICROCOD.3XX` against [the prerequisites](REQUIRED_EXTERNAL_FILES.md).

Copy from your own licensed known-good installation or original media. On
Windows 11, do not execute its 16-bit setup launcher. Do not substitute 4.8.9,
4.8.13 or a translated edition; analysis of those versions is not transport
qualification. Preserve the unmodified source tree as your recovery input.

The three exact **x86** VC++ 7.1 files must be together in the source application
root, `%WINDIR%\SysWOW64`, or an explicitly selected local runtime directory.
Setup copies them beside the private FlexColor.exe. No system-wide DLL
replacement or `regsvr32` is needed.

The manager uses a reversible four-site/five-byte backend-selection patch only
in `%LOCALAPPDATA%\USB2Xchange\App\out\flexcolor-aspi-private`. The active DLL hash
must be `D250B6177D2B30FADD55E06DF612E7F534CB5D1A30E82AA9CC71A3F1924DD119`;
the original backup must retain the original hash. The provider also checks
the executable, private-tree path, SRB structure and target/LUN before use.
A hash mismatch is a stop condition, not an invitation to edit constants.

Use **Repair installation** with FlexColor closed to recheck the sources and
restage the same provider/private patch. Start only using the project shortcut
or manager, not the original Program Files shortcut. This transport patch does
not change sharpening; see the separate [analysis](PRECISION_II_SHARPENING_ANALYSIS.md).
