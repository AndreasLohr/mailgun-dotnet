"""
Dump per-mutant detail (location + mutator + original vs replacement) for selected files.
Usage: python stryker-detail.py <substring-of-file-path> [Survived|NoCoverage|...]
"""
import json
import re
import sys
from pathlib import Path

REPORT = Path(
    r"c:\Users\andre\Documents\ClaudeCodeRoot\Libraries\mailgun-dotnet"
    r"\StrykerOutput\2026-05-26.13-23-38\reports\mutation-report.html"
)
html = REPORT.read_text(encoding="utf-8")
m = re.search(r"app\.report\s*=\s*\{", html)
start = m.end() - 1
depth = 0
in_str = False
esc = False
i = start
while i < len(html):
    c = html[i]
    if in_str:
        if esc:
            esc = False
        elif c == "\\":
            esc = True
        elif c == '"':
            in_str = False
    else:
        if c == '"':
            in_str = True
        elif c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                end = i
                break
    i += 1
report = json.loads(html[start : end + 1])

needle = sys.argv[1] if len(sys.argv) > 1 else ""
status_filter = sys.argv[2] if len(sys.argv) > 2 else "NoCoverage"

for path, fdata in report.get("files", {}).items():
    if needle.lower() not in path.lower().replace("\\", "/"):
        continue
    source_lines = fdata.get("source", "").split("\n")
    print(f"\n========== {path.split(chr(92))[-1]} ==========")
    matches = [m for m in fdata.get("mutants", []) if m.get("status") == status_filter]
    print(f"  {len(matches)} {status_filter} mutants\n")
    for mut in matches:
        loc = mut.get("location", {})
        s = loc.get("start", {})
        e = loc.get("end", {})
        line = s.get("line", 0)
        # Stryker line numbers are 1-based; show the affected source line.
        src_line = source_lines[line - 1] if 0 < line <= len(source_lines) else "(out of range)"
        print(
            f"  line {line:>3} col {s.get('column','?'):>3}  "
            f"{mut.get('mutatorName','?'):28s}  id={mut.get('id','?')[:8]}"
        )
        print(f"    src:         {src_line.strip()[:120]}")
        print(f"    replacement: {mut.get('replacement','?').strip()[:120]}")
        print()
