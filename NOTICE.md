# Notices, provenance and interoperability

Copyright © 2026 fcusb contributors. Project-owned material is licensed under
GNU GPL version 3 only (SPDX-License-Identifier: GPL-3.0-only). LICENSE is the
complete unmodified official FSF text. Its final example mentions a later-version
option; the project does not adopt that option. Project notices select version
3 only. No additional linking exception is granted.

Hasselblad, Imacon, FlexColor, FlexTight, Adaptec, USB2Xchange, Windows and
Microsoft are names/trademarks of their respective owners. Identification is
for interoperability and does not imply endorsement, sponsorship or ownership.
The project does not claim copyright in vendor components or their manuals.

## Source provenance and dependency review

The public tree contains project C#, C, IL, XML/INF configuration, PowerShell,
and documentation. There are no vendored libraries, NuGet packages, copied
Linux implementation files, embedded vendor executable/firmware resources, or
third-party runtime DLLs. It builds using separately installed Microsoft .NET
Framework, MSVC, SDK and WDK. Windows/.NET APIs and development tools are
separate platform prerequisites; their terms are not replaced by this GPL.
No project binary package redistributes those tools or the VC++ 7.1 runtime.

Protocol constants, ABI layouts, hashes, small exact patch-site signatures and
analyst behavioral descriptions are interoperability facts. Historical Linux
work informed the protocol, without copying substantial GPL implementation.
Kuna/Ghidra analysis informed human-written specifications, not pasted
reconstructed vendor implementation. Generated decompiler projects, vendor
binaries and private samples are excluded. The source boundary and
audits are recorded in docs/PUBLICATION_AUDIT.md.

No incompatible included third-party source dependency was identified by this
inventory/review. This is an engineering provenance review, not a legal opinion
or a guarantee about every jurisdiction's reverse-engineering rules.

## Proprietary loading boundary

The GPL provider is loaded in-process by the user's separately obtained,
locally patched FlexColor through its existing ASPI ABI. It exchanges SRB/CDB
structures and buffers with that program. The patch tool applies a small,
exact-hash-gated local modification to a reversible private copy. The public
repository supplies neither original nor patched FlexColor. It neither derives
FlexColor from this project nor licenses, endorses or relicenses FlexColor.

Dynamic loading and separate downloads are not by themselves proof of legal
separation under the GPL. Distribution of a combined application/provider
could raise GPL corresponding-source and proprietary-license questions that
this engineering review does not resolve. Do not redistribute the combined
private application tree. This source publication and project-only packaging
do not claim permission to distribute a combined work. Anyone planning such
distribution should obtain qualified advice and any necessary permissions.
See the [FSF GPL FAQ](https://www.gnu.org/licenses/gpl-faq.html) and
[GPL version 3](https://www.gnu.org/licenses/gpl-3.0.html).

## Binary correspondence

Each package embeds the exact public commit in
`USB2XCHANGE-RUNTIME-PACKAGE.json`; its adjacent manifest identifies the ZIP and
setup SHA-256. Convey project binaries only with access to their complete exact
corresponding public source, build/install scripts, LICENSE and notices. Retain
the commit and source archive alongside binaries. Builds are reproducible from
documented commands but byte-identical compiler output is not claimed.
