<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Contributing and support

Read the complete installation and troubleshooting guides first. This is an
experimental advanced-user project: individual installation support, recovery
assistance, compatibility and production fitness are not guaranteed. Questions
already answered by the documentation may be closed with a relevant link.

Use the issue templates. Include Windows edition/version/build, x64 status,
VM or physical-machine status and USB passthrough configuration, adapter
VID/PID/state, exact file hashes, exact commands and output, scanner identity,
whether it was powered, and snapshot/rollback status. Mark unsupported hardware
or FlexColor versions explicitly. Share only the smallest manually redacted
relevant log excerpt. Never attach proprietary executables, DLLs, firmware,
3F/FFF files, scans, TIFFs, manuals, raw buffers, traces, dumps or unrelated logs.

Small focused pull requests with a problem statement, evidence and appropriate
offline tests are welcome. Contributions to project-owned material must be
available under GPL-3.0-only; preserve attribution. Never paste decompiler
output or substantial vendor/GPL-2.0-only implementation into project source.
Document provenance and distinguish static findings, synthetic tests, inference
and hardware observations. Keep protocol code separate from Windows plumbing.

Run test.ps1 and source/binary audits before proposing a release. Hardware
experiments require a specific test plan, explicit operator authorization,
rollback and protected originals. A risk acknowledgement is not authorization
for arbitrary firmware patches or destructive SCSI commands.
