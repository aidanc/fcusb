<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Flextight Precision II scanner-RAM analysis

Date: 2026-08-27

## Result

Upgrading this scanner from 4 MiB to 16 MiB, and moving the motherboard
jumper accordingly, would provide **no benefit with the exact stock
`MICROCOD.3XX` image used by FlexColor 4.0.3**.

The motherboard and DMA logic can address extended physical memory, and the
firmware genuinely uses installed scanner RAM as a pool of acquisition
buffers. However, this firmware does not discover the installed capacity. It
allocates a fixed 3.5 MiB pool beginning at physical address `0x00080000`:

```text
pool base        0x00080000   512 KiB
pool size        0x00380000   3.5 MiB
exclusive end    0x00400000   exactly 4 MiB
```

There is no 2/4/16-MiB branch, capacity input, or later enlargement of that
pool in the recovered image. RAM above `0x00400000` is therefore not included
in the stock scan-buffer map. Merely installing a 16-MiB module does not make
the existing firmware use the additional 12 MiB.

This means the stock upgrade would not improve:

- image quality, optical resolution, bit depth, dynamic range, or sharpening;
- FlexColor processing, 3F/TIFF rendering, or file-save speed; or
- demonstrated scan throughput, buffering, or retry behavior.

A different or deliberately modified scanner image could, in principle, use
the larger memory as a deeper acquisition queue. That could let the scanner
run farther ahead of a bursty SCSI/USB host and reduce scanner-side flow
control. It would not change the pixels being acquired. No suitable 16-MiB
Precision II firmware has been found, and changing the one size constant is
not yet a safe patch: every descriptor-table consumer and memory boundary
would first need to be proven.

## Artifact and hardware binding

The analyzed scanner image is the user-supplied FlexColor 4.0.3 file:

```text
Firmware/MICROCOD.3XX
size:    65,536 bytes
SHA-256: d8d7188574c52b255cf7940bef7cc9192f744693aabc1f735758b7fecdec65f3
```

This is not the USB2Xchange adapter firmware. Sixteen observed 4,106-byte
WRITE BUFFER records reconstruct the exact file, and the terminal record
changed the real scanner from `Imacon / SCSI Loader / L302` to
`Imacon / FlexTight II / M333`. FlexColor's loader transfers this exact image;
the existing host and hardware observations show no capacity-dependent patch
of its bytes.

The [Precision II User's Guide](https://hasselbladrepair.com/wp-content/uploads/2016/12/FlextightPrecision2-Manual.pdf)
independently says the scanner firmware is downloaded when the application
starts. It also identifies the external rotary control as the SCSI address
selector. That is relevant because the firmware later reads an inverted
four-bit value from port `F860h`; it is not evidence of a RAM-capacity input.

## Ghidra and raw-assembly evidence

Kuna 1.115 rejects this raw image because it has no PE/ELF container. Ghidra
12.1.2 analyzes it as `x86:LE:16:Real Mode` at load segment `3000h`. The
existing 88-function project was re-opened read-only and a focused report was
preserved privately as an analyst evidence report (not redistributed).

The report includes the allocator, its only statically identified caller, the
scan-ring/DMA interrupt path, the DMA-address helper, cross-references,
instruction bytes, and indirect targets that initial auto-analysis missed.
The material findings are all verified against the raw file bytes.

### Fixed pool size and base

At startup, `3000:6669` first selects scanner-record index zero. Initialization
sets the allocator divisor to one at `3000:6112`, then calls the allocator once
at `3000:6141`.

`3000:63D4` initializes record-zero field `0x7644` to `0x4692`. In
`FUN_3000_3d6a`, raw assembly then performs:

```asm
3000:3DB7  MOV EAX,dword ptr [BX + 0x7644]
3000:3DBC  SHL EAX,1
3000:3DBF  MOVZX EBX,word ptr [0x866e]
3000:3DC8  DIV EBX
3000:3DCD  MOV EAX,0x00380000
3000:3DD8  DIV EBX
3000:3DDB  MOV [0x778a],AX
3000:3DDE  MOV dword ptr [BP - 4],0x00080000
```

In behavioral terms:

```text
block bytes  = (record[model].field_7644 * 2) / divisor
buffer count = 0x00380000 / block bytes
first buffer = 0x00080000
```

For the boot-time record-zero values this is:

```text
block bytes  = 0x4692 * 2 = 0x8D24 = 36,132
buffer count = floor(3,670,016 / 36,132) = 101
```

The loop at `3000:3DF8` writes each 32-bit physical buffer address into a table
at data offset `0x778C`, advances it by the block size, and stops at the
computed count. The final buffer starts at `0x003F2210`; its extent remains
below `0x00400000`.

The first nine cases at `3000:3E0E..3EB9` also divide their physical addresses
by 16 and store nine real-mode `segment:0000` pointers at
`0x85EC..0x860F`. This is strong structural evidence that these are working
memory buffers, not an unrelated timing constant. One recovered consumer at
`3000:433A` dereferences the eighth such pointer and examines buffer data.

### The acquisition path uses the fixed table as a circular DMA ring

Completing a second missed real-mode path at `3000:2B3D` establishes how the
full address table is consumed. The interrupt path checks producer/consumer
counters, reduces the current counter modulo the fixed buffer count at
`0x778A`, multiplies the remainder by four, and fetches the corresponding
32-bit physical address from `0x778C`:

```asm
3000:2B80  MOVZX EAX,word ptr [0x778a]
3000:2B86  ADD EAX,dword ptr [0x8610]
3000:2B8D  CMP EAX,dword ptr [0x8614]
...
3000:2BA5  MOV AX,word ptr [0x8614]
3000:2BAA  DIV word ptr [0x778a]
3000:2BAE  SHL DX,2
3000:2BB3  MOV EAX,dword ptr [BX + 0x778c]
3000:2BB8  MOV [0x8620],EAX
3000:2BBC  INC dword ptr [0x8614]
```

A mirrored branch at `3000:2BDF..2BF6` performs the same modulo/table lookup.
The selected address is then written byte by byte to DMA address ports
`F002h`, `F002h`, `F083h`, and `F085h` at `3000:2C34..2C5F`; the corresponding
block length is written at `3000:2C60..2C7C`.

The service/availability path independently applies the same bound at
`3000:31EE..3200`: it computes `count + producer - 1`, compares that with the
consumer counter, and branches when the ring has no slot available. The fixed
count therefore controls both address selection and flow control.

This closes the allocator-to-acquisition chain: the firmware does not merely
construct 101 plausible addresses, it cycles live scan DMA through exactly
that count and table. Because every table entry was generated from the fixed
3.5-MiB pool, this primary acquisition path cannot begin using RAM above
4 MiB just because a larger module is installed.

### Extended-memory-capable transfer logic

`FUN_3000_16dc` at `3000:16DC` programs a DMA-like I/O register bank. It
writes three count bytes and all four bytes of a caller-supplied physical
address:

```text
count:    F003h, F003h, F099h
address:  F002h, F002h, F083h, F085h
```

The raw `66h` operand-size prefixes and right shifts at `3000:1714..174B`
confirm 32-bit address handling. The scanner is therefore not limited to
640 KiB by its real-mode CPU; DMA/scan hardware is how it reaches the physical
buffer pool above conventional memory. This also makes a future larger pool
technically plausible.

It does not make the stock pool dynamic. `FUN_3000_3d6a` takes no capacity
parameter, contains no `IN` instruction, and has no branch around the
`0x00380000` immediate. Ghidra finds one direct caller. Package-wide raw-byte
search finds no 2-MiB, 4-MiB, or 16-MiB alternative used by this allocator.

### The four-bit port read is not a capacity override

`FUN_3000_174e` reads port `F860h`, inverts it, masks the result to four bits,
and saves it after the fixed pool has already been created. Later code writes
that value into the board I/O setup. The published hardware guide's external
0--6 SCSI selector matches this four-bit input. More importantly, regardless
of its name, it is temporally and structurally unable to change the earlier
allocator: no value from this function reaches `FUN_3000_3d6a`.

The physical 2/4/16-MiB motherboard jumper is therefore best understood as
memory-controller mapping/density configuration. It proves that the board can
be populated in more than one way; it does not prove that every downloaded
scanner image adapts its software buffer map. That distinction is an inference
from the user's motherboard observation plus the firmware, not a recovered
motherboard schematic label.

## What more RAM could have improved with different firmware

The current protocol traces establish the only credible benefit. Precision II
image data is streamed to the host as repeated selector-`28` READ(10) requests.
During the hardware-proven `60x60` full scan, 998 complete 4,494-byte image
responses were interleaved with 169 identical ten-byte short results. Mapping
those short results to BUSY/no-copy causes FlexColor to retry the same logical
read; their distribution identifies normal scanner-side producer/consumer
flow control rather than image data or a terminal record.

A hypothetical 16-MiB allocator retaining the same 512-KiB base would have a
`0x00F80000` (15.5-MiB) pool. With the boot-time `0x8D24` block size it would
describe 449 buffers instead of 101, about 4.45 times as many. The likely
effects would be:

- more tolerance for pauses or uneven latency in the SCSI/USB/host path;
- fewer short/BUSY retry cycles if buffer exhaustion is their cause; and
- possibly better sustained throughput when host delivery, rather than the
  CCD, mechanics, or exposure, is the bottleneck.

Those are **hypothetical benefits**, not behavior of the current image. The
internal 36,132-byte blocks are not the same unit as FlexColor's 4,494-byte
logical reads, so their counts must not be translated directly into scan rows.
Even with a larger pool, total scan time might barely change when mechanics or
exposure dominate. Image content and quality would remain unchanged.

## Recommendation

Keep the known-good 4-MiB module and jumper setting when running the exact
FlexColor 4.0.3/M333 firmware. Do not buy a 16-MiB module expecting a speed or
quality improvement from the stock scanner.

A defensible 16-MiB project would require one of:

1. an authentic Precision II firmware build whose allocator ends at 16 MiB;
   or
2. a separately reviewed firmware modification after proving the complete
   address-table bounds, all consumers, DMA behavior above 4 MiB, module type,
   refresh/timing requirements, and recovery path.

The naive `0x00380000 -> 0x00F80000` edit is not ready for hardware. It would
change a proprietary scanner image, invalidate the exact hash used by the
current loader safety gates, and could turn an incorrect assumption into RAM
corruption or a stalled mechanism. No firmware, installed software, jumper,
or scanner hardware was changed for this analysis.

## Confidence and remaining unknowns

| Finding | Evidence | Confidence |
|---|---|---|
| The exact M333 image uses scanner RAM for a physical circular DMA buffer pool from 512 KiB to below 4 MiB. | Ghidra + raw assembly at `3000:3D6A..3F2B`; modulo/table DMA consumer at `3000:2B80..2C7C`; first-nine far pointers. | High. |
| The stock image does not enlarge that pool for a 16-MiB module. | Immediate `0x00380000`; acquisition uses the resulting count/table; no allocator input/parameter/capacity branch; one identified caller; exact unmodified image sent to hardware. | High for this exact firmware. |
| Extra RAM would not alter quality, resolution, bit depth, or host USM. | Fixed acquisition-buffer role plus prior scanner/host sharpening and row-to-file analysis. | High. |
| A larger firmware-managed pool could reduce flow-control retries or host-induced stalls. | Current physical buffering design plus hardware-observed short/BUSY producer-consumer behavior. | Medium; not tested with a larger pool. |
| The 2/4/16-MiB jumper configures DRAM mapping/density rather than selecting a firmware code path. | User's motherboard observation plus absence of a capacity input in the downloaded image. | Medium-high; schematic or chipset documentation not yet recovered. |
| Another historical Precision II firmware may support 16 MiB. | No such image has been inventoried. | Unknown. |
