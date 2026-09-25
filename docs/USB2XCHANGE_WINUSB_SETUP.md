<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# One-time USB2Xchange WinUSB setup

> **EXPERIMENTAL SOFTWARE — USE AT YOUR OWN RISK.** Firmware loading can cause
> device malfunction, loss of access or hardware damage. No warranty or recovery
> is promised. Read [DISCLAIMER](../DISCLAIMER.md) and keep a rollback point.

Use only Adaptec USB2Xchange VID `03F3`, PID `2002` (loader) or `2003`
(operational). The older USBXchange PIDs 2000/2001 are not this configuration.
Keep the scanner off while preparing the adapter; create a rollback point.

1. Connect the adapter and open **USB2Xchange Manager → Adapter setup**.
2. If it says `NeedsWinUsb`, open Device Manager. Before binding, its description
   may be Unknown/Other devices and Code 28. In **Properties → Details → Hardware
   Ids**, verify `USB\VID_03F3&PID_2002`.
3. Select **Update driver → Browse my computer → Let me pick → Universal Serial
   Bus devices → WinUsb Device**. Choose Microsoft's signed inbox driver. Do not
   choose USB Mass Storage, a downloaded INF, or any legacy Adaptec driver.
4. Return to the manager. `NeedsInterfaceRegistration` means WinUSB is selected
   but the project GUID is missing. Click **Register interface**, read and accept
   the first-use warning, then accept the UAC prompt. It writes only the exact
   present adapter's interface GUID `{86A64B6A-BC77-49D2-B378-0F43E5DAA568}` and
   requests a device restart. Reconnect if Windows requests it.
5. `Ready` with PID 2002 means the loader can be opened. Click **Initialize
   adapter**. It uses only the exact supplied firmware and waits for PID 2003.
6. On first setup PID 2003 can appear unbound, so a timeout/message asking for
   the second binding can be expected. Check Hardware Ids again and repeat
   steps 2–4 for **PID 2003**. It should appear under Universal Serial Bus devices
   as WinUsb Device, service WinUSB, with the project GUID registered.
7. Close FlexColor before a cold USB reconnect. Windows should remember both
   bindings. Normal daily Start handles PID 2002 upload and PID 2003 discovery.

The firmware does not advertise `USB\MS_COMP_WINUSB`. Microsoft's
[automatic WinUSB documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/automatic-installation-of-winusb)
explains that compatible-ID mechanism. This unsigned community package cannot
silently bind on first connection. Fully automatic binding requires an
acceptably signed device-specific INF and is not part of this release.
[Microsoft's installation documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/winusb-installation)
provides background; this project uses the inbox selection above.

Diagnostic commands, from an extracted package root after acknowledgement and
binding (the second opens hardware and must only be run deliberately):

```powershell
.\out\bin\usb2xchange.exe list
.\out\bin\usb2xchange.exe load 'C:\local-inputs\usb2xchange.fw' --reenumeration-seconds 60
```

The first lists registered interfaces; an empty list does not prove the USB
adapter is absent in PnP. Use the manager/Device Manager to distinguish absent,
unbound, missing GUID and unhealthy devices. If multiple adapters are present,
disconnect extras before proceeding. See [troubleshooting](TROUBLESHOOTING.md).
