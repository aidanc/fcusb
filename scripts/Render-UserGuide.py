# Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
"""Developer-only: pip install Markdown==3.9; python scripts/Render-UserGuide.py.

The committed HTML is shipped unchanged; end users need no Python dependencies.
"""
from pathlib import Path
import markdown

root = Path(__file__).resolve().parent.parent
body = markdown.markdown((root / "docs/USER_GUIDE.md").read_text(encoding="utf-8"),
                         extensions=["tables", "fenced_code", "toc"])
document = """<!doctype html>
<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>USB2Xchange illustrated user guide</title>
<style>
:root{color-scheme:light}body{margin:0;background:#edf1f5;color:#203040;font:17px/1.65 system-ui,sans-serif}
main{max-width:920px;margin:auto;padding:40px;background:white}h1,h2{line-height:1.25;color:#122c45}
h1{font-size:2.2rem}h2{margin-top:3rem;border-top:2px solid #e1e8ee;padding-top:1.4rem}
a{color:#075cab}img{display:block;max-width:100%;height:auto;margin:24px auto;border:1px solid #b8c6d2}
table{border-collapse:collapse;width:100%;font-size:.94rem}th,td{text-align:left;vertical-align:top;border:1px solid #ccd6df;padding:10px;overflow-wrap:anywhere}th{background:#edf3f8}
pre{background:#edf3f8;padding:16px;overflow:auto}code{font-size:.88em}li{margin:.6em 0}
@media(max-width:650px){main{padding:20px}body{font-size:16px}h1{font-size:1.8rem}}
@media print{body{background:white}main{padding:0}h2{break-after:avoid}img,table{break-inside:avoid}}
</style><main>
""" + body + "\n</main></html>\n"
(root / "docs/USER_GUIDE.html").write_text(document, encoding="utf-8", newline="\n")
