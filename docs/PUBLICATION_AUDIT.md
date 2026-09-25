<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Public source publication audit

The public repository starts with a reviewed source-only tree and independent
Git history. No earlier private history, ignored artifact, vendor payload,
scanner buffer, trace, scan, log, manual or generated decompiler output is
included. The initial export contained only tracked source; internal journals,
handoff notes, obsolete operating instructions and machine-specific paths were
excluded or rewritten for public use.

Included: project-owned C#/C/IL components, PowerShell build/install/test
scripts, source INF/XML configuration, offline tests, GPL license/notices,
public setup/support/evidence documentation and issue templates. Historical
kernel research remains source-only and is not packaged for normal users.
Exact hashes and small patch/ABI/protocol constants are interoperability facts,
not embedded vendor implementation. See NOTICE for dependency and loading review.

Excluded: proprietary FlexColor/Adaptec binaries, all adapter/scanner firmware,
Microsoft runtime DLLs, proprietary manuals, sample images/3F, raw data/traces,
generated PDFs, analysis projects, private operational records and prior Git
objects. The existing generated sharpening PDF is intentionally not published.

Public validation results are recorded here before the initial push. Hardware
qualification is separately bounded by KNOWN_LIMITATIONS and FRESH_VM_ACCEPTANCE;
publication validation performs no USB/scanner operations.

## Review results (2026-09-25)

- The reviewed source is UTF-8 text: no NUL-bearing or binary dependency file.
  Whole-file hashes were compared with the known proprietary inputs; forbidden
  extensions and private/generated directory names were checked. Configuration
  files named `FlexColor.exe.config` and `wnaspi32.dll.config` are text, not DLLs.
- Targeted secret/content searches found no private paths/usernames, private
  repository URLs, GitHub/AWS token patterns, private keys or assigned-password
  patterns. Historical research approval strings are deliberate local mode
  guards, not account credentials. No dedicated history-aware secret scanner
  was installed; the new single-root history and actual export receive explicit
  text/hash/pattern review. This is not a guarantee that every conceivable secret
  or provenance issue can be detected automatically.
- All project-owned files carry GPL-3.0-only identification; no vendored
  third-party source dependency or incompatible included license was identified.
  Platform tools/runtimes are separately installed, not bundled. NOTICE records
  the unresolved legal implications of distributing a combined in-process
  FlexColor/provider; no proprietary combined distribution is offered.
- Official LICENSE was fetched from the FSF plain-text endpoint and preserved
  byte-for-byte: SHA-256
  `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986`.
  Its stock application example does not change this project's version-only
  selection. `.gitattributes` preserves its exact bytes on checkout.
- `git diff --check`, internal Markdown link validation, source-release audit
  and the explicit **46-file** binary allowlist audit passed. The allowlist
  includes license/notices and public operator/evidence documentation, with no
  vendor payload, firmware, kernel binary, certificate, PDB or log.
- Full `test.ps1` passed: protocol 25/25, patch 11/11, ASPI 78/78,
  broker protocol 35/35, plus broker helpers, native ABI, offline broker dry run,
  x86 SCSISCAN self-test, PowerShell workflow/manager guards and risk-gate tests.
  WDK `/W4 /WX` build and Inf2Cat completed with no warnings/errors. No driver
  was installed and no scanner path was opened by these tests.
- Risk tests verify rejection before dispatch, exact minimal persisted marker,
  stale/corrupt marker rejection and independent installation validation. The
  visual first-use dialog and elevation workflow still need clean-VM GUI tests.
- The local firmware extraction check reproduced all 16,492 bytes with the
  exact required D096… SHA-256 from the pinned Adpusbld.sys input. The generated
  firmware remains ignored and excluded from the public source/package.
- External acquisition URLs were checked; reachable landing pages are not
  claims of exact downloadable binary identity. See EXTERNAL_URL_REVIEW.

Package generation reruns the complete suite from its clean public commit and
tests bootstrap extraction, packaged manager startup and cleanup. The adjacent
artifact manifest records exact commit and setup/ZIP SHA-256; local candidates
are not a GitHub release. No tag/release version is adopted by these checks.
