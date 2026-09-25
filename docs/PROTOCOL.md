<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# USB2Xchange protocol and behavioral evidence

Scope: the supported USB2Xchange and exact Precision II/FlexColor combination,
not arbitrary USB mass storage or all SCSI devices. This is a human-written
interoperability specification, not vendor source. Addresses below are virtual
addresses in the exact original binaries. Names beginning `sub_`/`FUN_` are
analyst/decompiler names, not recovered original source names.

Evidence: **WIN** = original metadata/binary; **KUNA+ASM** = Kuna 1.115 recovery
checked against assembly; **LINUX** = historical independent implementation;
**HW** = observed hardware; **TEST** = device-free implementation tests;
**INFERENCE** = not yet established on hardware. No raw USB capture is published.

## Exact evidence artifacts

| Original artifact | SHA-256 |
|---|---|
| Adpusbld.sys, 27,472 bytes | `FA2629BDF855B5A320D2C184B40FFB2B780D8FDB67491504CEF2C5AA0E3E8381` |
| Win2000/2KAUSBST.SYS, 18,458 bytes | `1B6997FB7487B7985F3844B52735E4E22A3CEF9F9C8028536B91B2E1856F882D` |
| Win98/98AUSBMS.SYS, 40,592 bytes | `20E89922EEDF9C8579B865C4AC10B25D273A438DF6692CA4ECE0B485CD9CA6C7` |

User-supplied originals were inspected statically, not installed or executed.
Whole-project Kuna output and assembly were retained privately; they are not
redistributed. [Required inputs](REQUIRED_EXTERNAL_FILES.md) gives firmware and
FlexColor identities. [Reverse-engineering method](REVERSE_ENGINEERING.md)
describes the separation from implementation.

## Firmware and enumeration

**WIN + KUNA+ASM + LINUX + HW; high confidence.** USB2Xchange starts at
`03F3:2002` and re-enumerates as `03F3:2003`. The original USBXchange uses
2000/2001 and is not the released profile. In Adpusbld.sys, entry `0x102C0`
sets AddDevice `0x10536`, PnP/power `0x103BC` and unload `0x10468`. Start calls
loader `0x104D8`; hardware-ID comparison is at `0x1087E`.

The USB2 image table is at VA `0x12FE0`, file offset `0x2FE0`: 588 data records,
9,408 payload bytes, followed by one terminator. Each internal record is 22
bytes: length u8, reserved u8, address u16le, type u8, 16 data bytes, padding u8.
The external 28-byte record uses length/address/type u32le followed by 16 data
bytes. Exact conversion produces the 16,492-byte D096… firmware hash. This is
format conversion of a user-supplied file, not included firmware source.

At `0x10678` / `0x10700`, control OUT uses endpoint 0, request type `0x40`,
request `0xA0`, index 0, value equal to the target address, and length equal
to the record length (at most 16). CPU reset address is `0xE600`: byte 1 holds
reset, byte 0 starts execution. The recovered sequence is:

1. Hold reset twice.
2. Send all 588 records in order.
3. Hold reset once more, then release it.
4. Close the disappearing loader and wait boundedly for PID 2003.

Legacy upload stops on first negative result, without record retries. The exact
image ends below `0x2000`; the legacy external-memory `0xA3` branch is unused.
Kuna misrepresented one reset argument as uninitialized; assembly loads literal
1. The assembly is authoritative for that disagreement. A failed partial upload
is not permission to force CPU release or substitute firmware.

## Operational USB transport

**KUNA+ASM + firmware descriptors + HW; high confidence.** Interface 0, alternate
0, class/subclass/protocol FF/00/FF exposes bulk OUT `0x02` and IN `0x86`.
Packet sizes are 64 full-speed and 512 high-speed; 512 was observed. Discover
and validate descriptors at runtime. Win2000 functions `0x11C4A`, `0x11CBC`,
`0x12E2A` and `0x11D48` obtain/configure descriptors and select bulk pipes.
`0x12FCE`/`0x12FEE` send two zero-data control OUT requests: type `0x40`,
request `0x5A`, values 1 then 2, index 0. These are vendor initialization,
not standard BOT reset.

Win2000 entry `0x118A0` sets AddDevice `0x1190A` and StartIo `0x10914`.
The legacy driver serializes requests. The public transport likewise keeps one
complete SCSI command, including automatic sense, in flight per adapter.

### Command block, data and status

**KUNA+ASM + LINUX + HW; high confidence.** `0x10BA4` constructs 31-byte CBW;
`0x10C26`/`0x10CCC` transfer data; `0x10E5C` receives 13-byte CSW.

| CBW byte offset | Length | Meaning |
|---:|---:|---|
| 0 | 4 | `55 53 42 43`, USBC |
| 4 | 4 | Little-endian command tag |
| 8 | 4 | Little-endian requested data length |
| 12 | 1 | 80 for IN; 00 for OUT/no data |
| 13 | 1 | SCSI **target ID**, not standard BOT LUN |
| 14 | 1 | CDB length |
| 15 | 16 | CDB padded with zeros |

Sequence: `CBW OUT → optional DATA IN/OUT → CSW IN`.
CSW bytes 0–3 are `55 53 42 53` (USBS), 4–7 echo the tag, 8–11 are little-endian
residue, byte 12 is status. Validate exact wrapper length, signature, tag,
requested/actual sizes and residue. The Win2000 implementation did not visibly
check signature/tag; Win98 `0x131C9` checks signature. Public validation is
deliberately stricter than either incomplete legacy check.

Win2000 `0x109E9`–`0x10A03` encodes legacy SCSI-2 LUN in CDB byte 1 bits 5–7;
target stays in CBW byte 13. Current normal runtime allows only target 5/LUN 0.
Historical target/multi-LUN discovery is not present-day multi-device acceptance.

### Status, sense and short reads

**KUNA+ASM + LINUX; high confidence; success/short behavior also HW.**
Win2000 `0x10E90` interprets adapter status:

| Value | Meaning |
|---|---|
| 00 | completed |
| 02 | SCSI CHECK CONDITION (not BOT phase error) |
| 08 | SCSI BUSY |
| 8A | target selection timeout |
| other | unknown failure; do not silently accept |

`0x1105C` builds REQUEST SENSE while disabling recursive autosense. The new
provider preserves target/LUN and issues sense for CHECK CONDITION unless the
original command was already REQUEST SENSE. Device-free tests cover mapping;
authentic useful sense on this scanner remains unqualified.

**HW + TEST; high confidence for supported image streaming.** FlexColor did not
request residual count. Exact ten-byte short successful rows are returned as
BUSY with no partial copy, and FlexColor retries the logical row. When ASPI flag
0x04 requests residue, the implementation returns it and copies only actual IN
bytes. That flag behavior is device-free tested, not observed from FlexColor.

## Current trusted host policy

**Implementation/TEST, supported 6/10-byte application path HW.** The exact
private process may forward structurally valid 6/10/12/16-byte CDBs for target
5/LUN 0. Unknown flag bits, simultaneous IN/OUT, missing buffers, inconsistent
lengths, noncanonical CDB length and other LUNs fail before USB. Caps are 16 MiB
and 120 seconds per underlying USB transfer. Each SRB and sense are serialized;
overlapped timeout cancels I/O, and transport exceptions discard the retained
handle. The host policy is not a new wire-protocol fact or general SCSI writer.

The historic Linux warning that a 64-KiB device transfer may crash the adapter
conflicts with the legacy 64-KiB maximum-pipe setting. It does not establish
safety at any larger size. The 16-MiB validation ceiling is only an implementation
bound; accepted FlexColor streaming uses much smaller transfers. Do not infer
arbitrary transfer/CDB support from the ceiling.

## Scanner and image-processing boundary

**HW, high confidence.** The exact 65,536-byte scanner image MICROCOD.3XX is
sent as sixteen 4,106-byte records plus terminal record, changing target 5 from
`Imacon / SCSI Loader / L302` to `Imacon / FlexTight II / M333`. This scanner
image is distinct from adapter firmware. FlexColor owns normal initialization,
readiness, SET WINDOW, image reads and cleanup.

**HW, scoped acceptance.** Windows 10 transparent operation completed cancelled
Preview/cleanup, complete Preview and two same-process 60x60 scan/save cycles;
24x36 and 4x5 reached image flow then Stop/cleanup. Clean Windows 10 and 11
portable runtime runs subsequently passed scans and reconnect. See
[limitations](KNOWN_LIMITATIONS.md) for exact scope and unverified cases.

**Static + controlled file evidence.** The [sharpening report](PRECISION_II_SHARPENING_ANALYSIS.md)
identifies host direct-render +120, saved-file zero/off behavior and downloaded
CPU-image boundaries. [3F structure](3F_FILE_STRUCTURE.md) covers tested 4.8.9/949
files, not a verified Precision II-created sample. [RAM analysis](PRECISION_II_RAM_UPGRADE_ANALYSIS.md)
applies only to exact stock M333. No firmware/RAM modification is recommended.
