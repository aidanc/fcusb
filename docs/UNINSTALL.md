<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Uninstall and rollback

Back up scans outside the application tree first. Cancel/finish scanning in
FlexColor and let cleanup finish; close the owned process with Stop FlexColor.
Choose **Uninstall** in the manager or the per-user Windows Apps/Programs entry.

Uninstall deactivates the reversible private DLL patch and removes the private
FlexColor tree, stored adapter firmware, configuration, acknowledgement marker,
shortcuts and per-user uninstall entry. Logs under that private tree are removed;
save only necessary redacted excerpts beforehand. The licensed source remains
untouched.

Microsoft inbox WinUSB bindings and interface registration remain in place.
No project driver, certificate, system service or boot-policy setting is removed
because none is installed by the normal workflow. Removing the app does not
restore the earlier USB binding. For exact system rollback restore the
pre-binding VM snapshot, or use Device Manager's recorded previous binding and
rollback procedure. Do not install the XP driver on modern Windows as rollback.

For a script-only private tree, `Usb2Xchange-FlexColor.ps1 -Action Deactivate`
restores only the exact supported original DLL; it does not delete the package.
If exact-hash or path guards refuse cleanup, retain the error and original tree;
do not force recursive deletion of unverified paths. Full GUI uninstall on both
clean Windows VMs remains an acceptance item.
