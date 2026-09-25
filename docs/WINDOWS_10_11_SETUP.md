<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Windows 10/11 setup

Target: Windows 10 x64 and Windows 11 x64 desktop installations with Windows
PowerShell 5.1 and .NET Framework 4.8. Windows 10 build 19045 is the recorded
build environment. Earlier clean-VM runtime acceptance exists for both Windows
versions; edition and exact Windows 11 build were not preserved in the public
acceptance summary. No per-edition certification is claimed. Home/Pro/Enterprise
policy differences, future Windows builds, ARM64, x86 Windows, Windows 7, S mode,
and locked-down corporate configurations are not qualified.

Check `winver` and `$PSVersionTable.PSVersion`. Microsoft's [.NET installation
guide](https://learn.microsoft.com/en-us/dotnet/framework/install/guide-for-developers)
is the prerequisite route; obtain platform updates only from Microsoft. A newer
Visual C++ redistributable does not replace the exact VC++ 7.1 inputs.

For a VM, arrange USB passthrough for both VID 03F3 product IDs. A firmware
transition disconnects one device and attaches another; the host may capture
PID 2003 unless both states are assigned to the guest. Snapshot before binding.
On a physical machine, record the original device binding and keep an easy
rollback and backup. Do not alter SCSI connections while powered; follow the
manufacturer's termination, holder and electrical instructions.

The legacy FlexColor installer uses a 16-bit launcher, which cannot execute
on Windows 11 x64 (or native 64-bit Windows generally). The application itself
is PE32. Use your complete licensed installation tree as described in
[FLEXCOLOR_SETUP](FLEXCOLOR_SETUP.md); do not use unofficial compatibility
layers, replacement DLL sites or the XP Adaptec driver.

The normal package uses Microsoft inbox WinUSB. It never requires disabling
Secure Boot, signature enforcement, TESTSIGNING, or installing a self-signed
root. A process-local PowerShell `-ExecutionPolicy Bypass` in the documented
build commands does not set a machine-wide policy or override organizational
policy; inspect scripts before running them.
