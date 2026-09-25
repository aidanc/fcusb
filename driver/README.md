<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Historical kernel research source

Normal fcusb uses user-mode ASPI and Microsoft inbox WinUSB. See
[WinUSB setup](../docs/USB2XCHANGE_WINUSB_SETUP.md).
Set-Usb2XchangeInterfaceGuid.ps1 registers an existing WinUSB interface.

The miniport, broker ABI, INF and test-signing/install utilities are retained
as project-owned research source. They are built/tested offline but are not in
the user-mode package or normal installation. Do not enable test signing,
install certificates or load this research driver for normal use. Future kernel
experiments require separate explicit authorization and a disposable VM
checkpoint. Never install legacy Adaptec binaries.
