<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Flextight Precision II sharpening analysis

Date: 2026-08-26

Public scope note: file-layout observations apply directly to tested 4.8.9/949
files. Independent 4.0.3 host paths support the rendering distinction, but a
representative Precision II-created 3F remains unverified. See
[3F_FILE_STRUCTURE](3F_FILE_STRUCTURE.md). Proposed sharpening patches are static
feasibility only and are not implemented/released by fcusb.

## Result

The recurring `-120` advice is substantially correct for a **direct scanner
render**, but the usual explanation is wrong.

- No unsharp mask or other neighbourhood sharpening implementation was found
  in the Precision II scanner CPU image `MICROCOD.3XX`.
- Adjustable sharpening is implemented in the host-side FlexColor DLL.
- In both FlexColor 4.0.3 and 4.8.9, the ordinary `CaptureType == 1` path adds
  decimal `120` to the selected USM Amount before building the sharpening gain
  table. Clearing Apply first selects zero, but the same `+120` is then added.
- Consequently, on that direct-capture path, Apply-off and UI Amount `0` both
  become internal Amount `+120`; UI Amount `-120` becomes internal Amount `0`
  and is the genuine no-USM value.
- A scanner 3F contains more than one raster. Its full-resolution archival
  raster is scanner RGB saved before FlexColor's USM/render stages. It is not
  sharpened by the hidden `120`. A separate processed 8-bit preview can show a
  sharpened rendition without changing that archival raster.
- Rendering the saved 3F uses the other capture-type path and does not receive
  the `+120` adjustment. There, Apply-off or Amount `0` is neutral and `-120`
  is active softening.

Thus `-120` does not send a firmware command or disable sharpening in the
scanner. It cancels a hidden host-software offset used while FlexColor renders
a scan directly. The folklore that this sharpening is irreversibly baked into
the full-resolution 3F raster is false for the analyzed scanner 3F format and
FlexColor paths.

This is definitive for the downloaded CPU image and the recovered FlexColor
paths. Static analysis cannot by itself exclude ordinary optical edge response,
analog ringing, or fixed processing in unidentified scanner logic outside this
x86 image. Those possibilities are narrower hardware-MTF questions, not the
source of FlexColor's exact `120` behavior.

## Exact artifacts

The user-supplied licensed FlexColor 4.0.3 tree contains the scanner image:

```text
Firmware/MICROCOD.3XX
size:    65,536 bytes
SHA-256: d8d7188574c52b255cf7940bef7cc9192f744693aabc1f735758b7fecdec65f3
```

Sixteen observed 4,106-byte loader records reconstruct this exact file, and the
terminal loader record changed real-hardware identity from
`Imacon / SCSI Loader / L302` to `Imacon / FlexTight II / M333`. This binds the
analyzed bytes to the Precision II scanner. It is separate from the
USB2Xchange adapter firmware.

The host DLLs independently inspected were:

```text
English FlexColor 4.0.3 DLLS/FlexColor.dll
SHA-256: b49217ba2bbff2e9a9df0952cc9657cd197c10022e2b62e3b818719fb78c1e84

FlexColor 4.8.9.1 DLLS/FlexColor.dll
SHA-256: 429d3992ec6315b1cfc1a474f22e87010fefdac6eeb48483e5eceab05c79a2e4
```

The project's active private 4.0.3 DLL has four ASPI backend-selection patches.
Those transport-only edits are unrelated to image processing, so the original
DLL was used for this analysis.

## Scanner firmware structure

Kuna 1.115 was tried first. It rejected `MICROCOD.3XX` as an unknown object
format because this is a raw memory image, not PE or ELF. Ghidra 12.1.2 then
analyzed it as `x86:LE:16:Real Mode` at load segment `3000h`. Generated
artifacts are retained under ignored private analysis directories; 88 functions
were recovered.

The entry at file offset `001Eh` is `CLI; CLD; JMP FAR 3100:0000`. Startup at
file offset `1000h` sets the stack, copies `75B0h` initialized bytes from
segment `368Bh` to `4000h`, clears BSS, and calls the controller at runtime
`312F:5375` (file offset `6665h`). The identity at `002Dh` is
`Imacon  FlexTight II    M333`.

The smooth periodic tables following the identity feed motor-step output
paths. Initialization configures motion, timing, geometry, buffers, and I/O
ports; the main controller dispatches seven event classes through the table at
file offset `6864h`.

### Data-path evidence

The principal payload paths are byte-preserving:

- `3000:5D59` reads payload bytes after a ten-byte request header and writes
  each byte unchanged to an I/O port.
- `3000:5DCA` obtains source, destination, and length, then uses `REP MOVSB` at
  `3000:5E34`.
- `3000:5E3F` selects one of those paths from a direction flag.

The only repeated string operations are the startup copy/BSS clear and that
request-payload copy. No CPU loop combines a pixel with left/right or
previous/next-row neighbours. Of 40 recovered `IMUL` instructions, 24 index
62-byte scanner records, five index 11-byte records, and the remaining genuine
sites perform structure indexing or motion/timing geometry. One apparent site
is the ASCII identity misdecoded as code. Division sites likewise belong to
timing, scaling, or geometry paths.

No supplied firmware file contains `sharp`, `unsharp`, `USM`, `ApplyUSM`,
`USMAmount`, `radius`, or `grain limit`. Known constants from the recovered host
filter (`298`, `587`, `114`, `10000`, `17356`, and `1638400`) are absent. The
sole little-endian two-byte occurrence of decimal `3678` is part of the
addressing operand for `LES BX,[BP+0Eh]` at `3000:5908`, not a coefficient.

Negative searches alone would not prove absence. The high-confidence firmware
conclusion combines the complete function map, raw assembly, transfer data
flow, arithmetic classification, and independent recovery of the actual filter
in the host DLL.

## Host-side sharpening and the hidden 120

Both DLLs contain the RTTI class `CSharpenYcc` and the setting names
`ApplyUSM`, `USMAmount`, `USMRadius`, and `USMDarkLimit`. Their setup and
gain-table routines are structurally equivalent.

### FlexColor 4.0.3

At `0x70298580`, raw assembly performs this sequence:

1. If Apply is set (`settings+0089h`), copy signed `USMAmount` from
   `settings+09E2h`; otherwise set the working Amount to zero.
2. Unless a separate camera/special-mode helper handles the amount, compare
   the capture-mode word at `settings+09D4h` with `1`.
3. If it is `1`, execute `ADD dword ptr [EBX],78h` at `0x70298753`.
4. Call the gain-table builder at `0x702987B0`.

The DLL also contains the parallel sharpen-and-blur implementation at
`0x70298060`. It performs the same capture-state comparison and executes
`ADD dword ptr [EBP],78h` at `0x702981F9`. Both implementations must be
covered by any patch; changing only the more easily identified `CSharpenYcc`
site is incomplete.

The settings identity is cross-checked by constructors, setters, neighboring
USM defaults, and capture-mode paths. Amount is at `+09E2h`; defaults set it to
zero, while the neighboring fields receive Dark Limit `10` and Noise Limit
`0`. The `+09D4h` word is initialized and switched between the direct-capture
and file-processing states independently of Film Type. Film Type is a separate
field used later to reverse the gain table for negatives.

In particular, the direct setup at `0x702036C0` writes state `1` at
`0x702037D3`, while the saved-file render routine at `0x7026D230` writes state
`2` at `0x7026D37C` before the image-processing calls. This independently
establishes the same direct-versus-file distinction in 4.0.3.

The builder at `0x702987B3..0x702987D3` loads the adjusted working Amount. If
it is non-positive, it stores that exact signed value into all `4000h` table
entries. This proves that the only neutral working Amount is zero.

### FlexColor 4.8.9

The later DLL independently confirms the same rule. `CSharpenYcc` setup at
`0x7036DE70` reads `USMAmount` from `settings+11F2h`, then at
`0x7036E010..0x7036E01A` compares `settings+11E4h` with `1` and executes
`ADD dword ptr [EBX],78h`. The gain-table builder at `0x7036D590` again fills
all 16,384 entries with the exact adjusted Amount when it is non-positive.

The settings constructor at `0x702D4360` initializes `settings+11E4h` to `1`.
The file/FFF render path at `0x7033EEB0` explicitly changes it to `2` before
image processing. This is why the same UI value has different meaning in a
direct scan and a saved-file render.

The later DLL again has a parallel sharpen-and-blur implementation at
`0x705142A0`. Its equivalent capture-state branch executes
`ADD dword ptr [EBX],78h` at `0x7051447D`. As in 4.0.3, both offset sites are
live implementation alternatives and a robust patch must cover both.

### Effective values

For the ordinary paths recovered here:

| Context | UI/Apply state | Working Amount | Result |
|---|---:|---:|---|
| Direct scanner render (`CaptureType=1`) | Apply off | `0 + 120 = 120` | sharpening remains active |
| Direct scanner render (`CaptureType=1`) | Apply on, Amount `0` | `0 + 120 = 120` | sharpening remains active |
| Direct scanner render (`CaptureType=1`) | Apply on, Amount `-120` | `-120 + 120 = 0` | neutral/no USM correction |
| Direct scanner render (`CaptureType=1`) | Apply on, Amount `x` | `x + 120` | active at shifted amount |
| Saved 3F/file render (`CaptureType=2`) | Apply off or Amount `0` | `0` | neutral/no USM correction |
| Saved 3F/file render (`CaptureType=2`) | Amount `-120` | `-120` | active softening |

The special camera-mode helper can override this generic rule; it is not the
ordinary Precision II scanner path.

## Is sharpening baked into a 3F?

No—not into the full-resolution archival raster. The word “3F” is often used
for three different things that must be kept separate:

| 3F component/use | Tested scanner-file role | USM conclusion |
|---|---|---|
| IFD 0 | full-resolution, uncompressed RGB16 scanner plane; tested values are 14-bit codes left-aligned in 16-bit words | before FlexColor rendering and USM; no hidden `120` is baked into these pixels |
| IFD 1 | processed RGB8 preview | a rendered convenience image; it may reflect sharpening/settings and must not be mistaken for the archival source |
| IFD 2 | reduced scanner-domain RGB16 preview | measured as a near-exact box reduction of IFD 0; useful for inspection, not the full-quality source |
| TIFF/JPEG exported from the 3F | newly rendered output from IFD 0 plus the selected settings | USM is applied at export only when the file-render settings request it; zero/off is neutral |

The settings/history block can contain `ApplyUSM`, `USMAmount`, radius, and
limits. That records a possible/default rendition; it does not prove those
settings were destructively applied to IFD 0. This distinction is independently
supported by all of the following:

- Hasselblad describes 3F as an extended TIFF containing raw 16-bit data,
  settings history, and a preview, and says export settings leave the original
  image data unchanged.
- Inspection of three FlexColor 4.8.9 Flextight 949 files found the separate
  full-resolution scanner plane and processed preview described above.
- The recovered 4.8.9 file-render path switches the internal capture state from
  `1` to `2` before constructing the image filters, bypassing `+120`.
- A byte-exact independent renderer starts from IFD 0 and then performs the
  tone and USM stages; it does not need to undo pre-existing USM.
- The generic TIFF scanline writer serializes the `CxImage` buffer it is given
  and contains no call to `CSharpenYcc`; sharpening is a separate render filter,
  not an operation hidden inside TIFF/3F serialization.

Therefore an apparently sharpened 3F view can be its processed preview or its
current/default rendition. That appearance is not evidence that the
full-resolution source pixels have been irreversibly sharpened.

## Controlled 3F/file-render result

A FlexColor 4.8.9 native-export probe rendered the same full-resolution FFF
with Apply off, Apply on with Amount zero, and the tested spatial stages off.
All three 14,189,186-byte TIFFs had SHA-256:

```text
3638a921354b9f1a3604cb9990ed4c8713602fc23aebe572e5e837dc76e8b8ec
```

The FFF explicitly contains `ApplyUSM=true`, `USMAmount=0`, and persisted
`CaptureType=0`; the native file-render code maps this to its non-direct state
(`settings+11E4h = 2`). Thus the byte-identical output supports the recovered
distinction: off and zero are neutral when rendering a saved 3F, but this test
does not contradict the direct-capture `+120` branch.

The 4.8.9 host algorithm has also been reproduced end-to-end against reference
TIFFs. It derives YCC luminance, forms a local separable blur, looks up a
luminance-dependent gain, and adds the signed detail correction to R, G, and B.
This is host pixel processing, not scanner microcode.

## Patch feasibility for 4.0.3 and 4.8.9

A minimal host patch is feasible for both versions. It is **not needed to
protect IFD 0 in a 3F**. Its purpose would be to make the direct-capture UI
literal: off/Amount `0` would mean neutral instead of receiving the hidden
`+120`. It may also remove that baseline from a processed preview built through
the same direct-capture state.

The safest instruction-preserving transformation is to change only the signed
immediate `78h` to `00h`, turning each `ADD working_amount,120` into
`ADD working_amount,0`. Two sites are required in each DLL:

| Version / original SHA-256 | Function/site VA | File offset | Original instruction bytes | Proposed bytes |
|---|---:|---:|---|---|
| 4.0.3 / `B49217BA...C1E84` | `0x702981F9` | `0x2981F9` | `83 45 00 78` | `83 45 00 00` |
| 4.0.3 / same | `0x70298753` | `0x298753` | `83 03 78` | `83 03 00` |
| 4.8.9.1 / `429D3992...A2E4` | `0x7036E01A` | `0x36D41A` | `83 03 78` | `83 03 00` |
| 4.8.9.1 / same | `0x7051447D` | `0x51387D` | `83 03 78` | `83 03 00` |

Read-only validation found the expected bytes at every site. Neither installed
DLL has an Authenticode signature. Applying both one-byte immediate changes in
memory, without writing a file or changing the PE checksum field, predicts
these exact output hashes:

```text
4.0.3:   fa15dc6084a38c62722fd379cb3afbe02a67ad74a102e4c9c6c7cfe3cefbcb76
4.8.9.1: 5af7bb2fa738e65c17575414119be9118fe65a046728f1ec5ed932cc999a568c
```

The 4.0.3 DLL's PE checksum field is zero and can remain zero. The 4.8.9.1 DLL
has an existing checksum `00A1A112h`. Recomputing it with Windows
`CheckSumMappedFile` after the two instruction edits produces `00A22899h` and
final SHA-256
`a0b0b618578a2ed77317fec91678e31658b3c34aa97b6643224f2ab91d0a6482`.
A future qualified 4.8.9 manifest would need that rechecksummed result. Microsoft
documents that ordinary application DLLs are not among the images whose
checksum is necessarily validated at load time, but also recommends that
modified PE images carry a valid checksum; maintaining it avoids an unnecessary
integrity discrepancy.

The implementation plan is:

1. Build a source-only patcher with separate exact manifests for these two DLL
   hashes, lengths, PE32/I386 identity, image base, all surrounding instruction
   bytes, checksum policy, and the expected final hash. Refuse every other file.
2. Patch a private staged FlexColor tree, never the installed Program Files
   copy. Keep and hash a byte-for-byte original alongside it; provide explicit
   `status`, `activate`, and `deactivate` operations.
3. For 4.0.3, compose the sharpening transformation from the clean original
   together with the existing ASPI compatibility transformation. Do not stack
   untracked edits onto the already transport-patched DLL.
4. Validate with deterministic input: patched direct Amount `0`/off must match
   unpatched direct Amount `-120`; a positive amount must still sharpen; saved
   3F Amount `0` export must remain byte-identical; and the 3F IFD 0 raster must
   be identical before and after the host patch.
5. Exercise both rendering implementations and compare embedded preview pages
   separately from IFD 0. Then perform one checkpointed real Precision II run
   on 4.0.3 and one 949 run on 4.8.9 before treating either patch as released.

This patch changes only FlexColor's direct-render interpretation of Amount. It
does not change scanner firmware, USB/SCSI behavior, focus, acquisition data,
or the saved-3F file-render branch. A more invasive “always bypass USM” patch is
possible but not recommended because it would also remove deliberately selected
sharpening and would require more control-flow changes.

## Documentation and community claims

Hasselblad's official [FlexColor 4 scanner
manual](https://cdn.hasselblad.com/0b68568e-5c54-4989-a1b6-a38f46177406_flexcolor-manual-scanners.pdf)
says the Apply checkbox turns the USM filter on or off and instructs users to
clear it to disable the filter. That describes the UI contract but omits the
direct-capture `+120` adjustment present in both analyzed implementations.

Historical community advice distinguishes direct TIFF from saved-3F rendering.
The conclusions here rest on identified assembly and controlled file results,
not on an unverified secondary support quotation.

The publisher's [Precision II hardware
manual](https://www.hasselbladrepair.com/wp-content/uploads/2016/12/FlextightPrecision2-Manual.pdf)
separately confirms that firmware is downloaded when the scanner software
starts. It does not assign sharpening to that firmware.

Microsoft's [`IMAGE_OPTIONAL_HEADER32`
documentation](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-image_optional_header32)
identifies the images whose checksums Windows validates at load time, and its
[`CheckSumMappedFile`
documentation](https://learn.microsoft.com/en-us/windows/win32/api/imagehlp/nf-imagehlp-checksummappedfile)
recommends updating the checksum when an executable image is modified.

## Remaining hardware-level question

The downloaded CPU firmware contains no identified sharpening path, but the
complete physical acquisition chain has not yet been measured for overshoot or
ringing. To settle that narrower issue on this exact scanner:

1. scan a clean slanted or knife edge at native optical resolution;
2. retain raw SCSI RGB48 rows and compare them byte-for-byte with FFF page zero;
3. render the same FFF with file-path Apply off/zero and `-120`;
4. measure edge-spread, line-spread, overshoot/undershoot, and MTF; and
5. repeat acquisitions to separate deterministic response from noise and
   registration differences.

That experiment moves the holder and changes scanner state, so it requires a
fresh checkpoint and explicit operator approval. It is not needed to answer
the software question: **the scanner firmware does not implement the recovered
sharpening; FlexColor does, and `-120` is the correct cancellation value for a
direct scanner render, while zero/off is correct for a saved 3F render.**
