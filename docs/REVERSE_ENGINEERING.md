<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Reverse-engineering method and publication boundary

Recover externally relevant behavior, write an evidence-backed specification,
then implement independently against documented platform APIs. Decompiler
output is neither original source nor ground truth. Do not mechanically port
or publish reconstructed vendor implementation.

The legacy study used Kuna 1.115 whole-project decompilation and function
inventories for seven PE files. Interesting functions were checked against
assembly, imports, constants, original INF metadata and historical Linux work.
Kuna's raw-image limitation required Ghidra 12.1.2 for the M333 real-mode x86
image, with raw instruction checks. Analyst-assigned names must be identified
as such and tied to exact artifact hashes/addresses.

For each new protocol claim record binary SHA-256, function/address, observed
behavior, independent check, evidence type, confidence and unresolved questions.
Distinguish metadata/binary, Kuna+assembly, Linux history, actual hardware,
USB traces, synthetic tests and inference. Never promote a static finding into
physical-hardware proof. Document disagreements instead of choosing convenient
decompiler output.

Privately preserve original provenance and hashes, PE metadata, imports,
exports, strings, Kuna function inventory, generated C/headers/assembly and
errors. Generated analysis, originals, raw buffers/traces/scans and proprietary
manuals must stay outside the public repository. Public files contain only
human-written behavioral summaries, small interoperability signatures and
project-owned implementation. Never execute a legacy installer or kernel
binary merely to inspect it.

The initial legacy protocol evidence and function mapping is retained in
[PROTOCOL](PROTOCOL.md); the exact sharpening/RAM evidence has dedicated reports.
The historical Linux USBXchange implementation informed target/status quirks;
no substantial implementation was copied. Firmware availability in a research
archive does not establish redistribution permission.
