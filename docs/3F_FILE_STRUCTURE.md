<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Tested scanner 3F/FFF structure

This is a reviewed interoperability report, not an official format specification.
**FILE-OBSERVED** refers to three privately held FlexColor 4.8.9 Flextight 949
scanner files. **STATIC** refers to exact host code paths checked against
assembly. **INFERRED** means reasoned interpretation. **UNKNOWN** means not
verified. No sample, private metadata record, raster excerpt or vendor code is
distributed here. A representative Precision II-created 3F remains unverified.

Hasselblad describes 3F as extended TIFF with source data, settings/history and
preview; see the [official scanner manual](https://cdn.hasselblad.com/0b68568e-5c54-4989-a1b6-a38f46177406_flexcolor-manual-scanners.pdf).
The detailed layout below is from file inspection, not a promise that every
scanner/camera/software version has the same layout. `.fff` is not `.3fr` and
the extension alone is not a sufficient signature.

## Container and rasters

**FILE-OBSERVED, high confidence within the tested set.** The files are classic
TIFF, big-endian (`MM`, magic 42), with a stored 32-bit offset to the first IFD.
Private records can precede the first IFD; readers must follow the offset,
not assume directory 0 starts at byte 8. These files are not BigTIFF.

```text
TIFF header -> IFD 0 -> IFD 1 -> IFD 2
                |        |        |
             source   processed  reduced scanner-domain
              RGB16    RGB8           RGB16
         + private settings/history records
```

| Page | Observed role and storage | Confidence / boundary |
|---|---|---|
| IFD 0 | Full-resolution, uncompressed, chunky/interleaved unsigned RGB16; one strip, width×height×6 bytes | FILE-OBSERVED, high. Source scanner domain before host rendering; not demonstrated literal CCD/ADC output. |
| IFD 1 | Reduced processed RGB8 preview, width×height×3 bytes | FILE-OBSERVED, high. Can include tone/sharpening rendition and is not the archival source. |
| IFD 2 | Reduced scanner-domain RGB16; measured as near-exact box reduction of IFD 0 | FILE-OBSERVED, high for files; reduction algorithm attribution is inference. |
| Export | New TIFF/JPEG rendered from source plus chosen settings | STATIC + controlled export, high for tested host paths. Not another name for IFD 0. |

All observed pages use Compression=1, PhotometricInterpretation=RGB,
SamplesPerPixel=3 and one strip. BitsPerSample is 16/16/16, 8/8/8, 16/16/16
respectively. Unsigned SampleFormat is implicit where absent. NewSubfileType
alone is insufficient: the reduced scanner-domain page also carried zero.
Use geometry, storage tags and private evidence, not just page index or appearance.

**FILE-OBSERVED, high; not universal sensor precision.** In tested IFD-0 samples
the low two bits are zero: `stored_u16 = code14 << 2`. Thus effective codes are
0–16383 left-aligned in 16-bit words (0–65532, steps of four). BitsPerSample
still says 16. Validate this condition before shifting; do not normalize by an
image's observed maximum or assume the same convention for Precision II.
Big-endian sample order must be respected on little-endian Windows.

## Settings, previews and rendition

**FILE-OBSERVED + STATIC, high.** Private FlexColor records contain rendering
settings and history, including ApplyUSM, Amount, radius and limits. Their
presence records a possible/default rendition, not proof of destructive edits
to IFD 0. A viewer may show IFD 1 or render a current rendition from IFD 0;
either can look sharpened while source samples remain unchanged. Private tag
numbers seen include 0xC519, 0xB4C5, 0xB4C7 and 0xC51A. This report deliberately
does not publish complete records or claim an exhaustive private-tag schema.

**STATIC + controlled file renders, high for host USM.** FlexColor 4.8.9's
settings constructor initializes its capture state to 1; file rendering at
0x7033EEB0 sets `settings+0x11E4` to 2 (write at 0x7033F008). A stored
CaptureType of zero is therefore not the runtime state used by the filter.
The file path avoids the direct-scanner +120 Amount adjustment. Controlled
4.8.9 saved-FFF exports with Apply off and Amount zero were byte-identical.

**STATIC, independently supported in 4.0.3.** Direct setup writes state 1 at
0x702037D3; saved-file rendering writes state 2 at 0x7026D37C. Both recovered
direct-render implementations add 120 only in the direct state, at 0x702981F9
and 0x70298753. These code paths support the host separation; they do not prove
the exact IFD layout/bit alignment of a Precision II-created 3F. Exact DLL hashes
and analysis are in [the sharpening report](PRECISION_II_SHARPENING_ANALYSIS.md).

USM can be recorded as settings without changing source samples. To demonstrate
this for any new file/software combination, compare source-strip bytes before
and after a settings-only edit, separately from previews and private metadata.
Do not compare the whole-file hash and conclude source pixels changed merely
because settings/history or previews changed.

## Safe inspection

Work on a duplicate, hash the original first, and use a read-only TIFF parser.
Inspect byte order, IFD links, dimensions, sample depth, photometric format,
strip offsets/counts and private-tag presence before decoding. Bound all
offsets/counts against file size, reject overflow/IFD cycles, cap allocations,
and deliberately reject unsupported compression/planar/tiled variants. A small
crop or streamed strip digest avoids allocating huge rasters. Inspect each page
separately; never overwrite the source or export private metadata wholesale.

For 14-bit validation, test low bits across the actual source samples, excluding
padding, metadata and previews. Record only aggregate results/hashes, without
publishing pixels or personal metadata. Verify file-copy identity again after
read-only inspection. Do not upload personal 3F files to a public issue.

**UNKNOWN:** the complete optical/analog/ASIC/FPGA acquisition chain, radiometric
linearity, every pre-FFF calibration operation, and representative Precision II
file layout. “Scanner-domain source” means before the examined host rendering
stages; it does not assert physically unprocessed sensor data. A sharpened-looking
preview alone proves none of these unknowns.
