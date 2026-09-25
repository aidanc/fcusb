<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Guide image provenance

These five PNGs are project-owned Windows Forms screen exports from the real
manager in explicit demo mode, produced on 2026-09-25. They show in-memory
example paths/states only, not a connected scanner or proprietary FlexColor UI.
No desktop, user name, licensed file contents, USB data or scan appears in them.
They are covered by GPL-3.0-only alongside the application and guide.

The desktop capture helper failed on this VM with `SetIsBorderRequired failed:
No such interface supported (0x80004002)`. Instead the application rendered its
own visible forms using `Form.DrawToBitmap` after layout and Shown processing.
These are actual application-rendered screens, not artist-created mockups.

To reproduce from the project root after building, use a new empty output folder:

```powershell
.\out\bin\usb2xchange-manager.exe --demo-export C:\guide-images
```

This explicit documentation command writes only its five PNGs and refuses to
overwrite existing ones. Ordinary `--demo` writes no setup files or settings;
all demo hardware/install actions remain in memory. Windows fonts/themes and
scaling can change the rendered bytes. Open/export the forms on Windows;
no hardware, firmware or FlexColor input is needed.

| File | SHA-256 |
| --- | --- |
| `01-manager.png` | `B88828B6686719CE8F13880ADA784D25518173DF4016B8A80350E0625CAF1C33` |
| `02-setup.png` | `9C66C23C593D4756E8ED13AC660B71299E4D00CEF6C9B52D23CD8C84CD6C7190` |
| `03-adapter-loader.png` | `F469CFB3DD3BC6A28F775FF00BC8D4561805C70504E5E9ABE556AEB75A90D75C` |
| `04-adapter-ready.png` | `2825E48951114FF5B339389EF35E5367EB8D3F321E8107F6D7FB9035C7BDABC8` |
| `05-session.png` | `7368F11E7F85E48882A506BBB154D8BF2AB35E0DDBF8035433DC3E30FABE6A5B` |
