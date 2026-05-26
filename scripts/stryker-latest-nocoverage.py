"""Dump every NoCoverage mutant from the latest Stryker run with file + line + mutator + replacement."""
import json
import re
from pathlib import Path

# Find the most recent StrykerOutput run that has a report.
root = Path(r"c:\Users\andre\Documents\ClaudeCodeRoot\Libraries\mailgun-dotnet\StrykerOutput")
runs = sorted([d for d in root.iterdir() if d.is_dir()], key=lambda d: d.name, reverse=True)
report_path = None
for d in runs:
    candidate = d / "reports" / "mutation-report.html"
    if candidate.exists():
        report_path = candidate
        break
assert report_path is not None, "No Stryker report found"
print(f"Using report: {report_path}")

html = report_path.read_text(encoding="utf-8")
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

totals = {"Killed": 0, "Survived": 0, "NoCoverage": 0, "Timeout": 0, "CompileError": 0, "Ignored": 0}
files = report.get("files", {})
nocoverage_per_file = {}
for path, fdata in files.items():
    src_lines = fdata.get("source", "").split("\n")
    for mut in fdata.get("mutants", []):
        s = mut.get("status", "?")
        totals[s] = totals.get(s, 0) + 1
        if s == "NoCoverage":
            nocoverage_per_file.setdefault(path, []).append((mut, src_lines))

print("\n--- Totals ---")
for s, n in totals.items():
    print(f"  {s:14s} {n}")

reached = totals["Killed"] + totals["Survived"] + totals["Timeout"] + totals["NoCoverage"]
covered = reached - totals["NoCoverage"]
score = (totals["Killed"] + totals["Timeout"]) / reached * 100 if reached else 0
covered_score = (totals["Killed"] + totals["Timeout"]) / covered * 100 if covered else 0
print(f"  Mutation score:  {score:.1f}%   ({reached} reached)")
print(f"  Covered-code:    {covered_score:.1f}%   ({covered} covered)")

print(f"\n--- {totals['NoCoverage']} NoCoverage mutants ---")
for path, items in sorted(nocoverage_per_file.items()):
    short = path.replace("\\", "/")
    short = "src/Mailgun" + short.split("/src/Mailgun", 1)[1] if "/src/Mailgun" in short else short
    print(f"\n  {short}  ({len(items)})")
    for mut, src_lines in items:
        loc = mut.get("location", {})
        line = loc.get("start", {}).get("line", 0)
        col = loc.get("start", {}).get("column", 0)
        src = src_lines[line - 1].strip() if 0 < line <= len(src_lines) else "(out of range)"
        print(f"    L{line:>4} col {col:>3}  {mut.get('mutatorName','?')[:35]:35s}  id={mut.get('id','?')[:10]}")
        print(f"      src:         {src[:110]}")
        print(f"      replacement: {mut.get('replacement','?').strip()[:110]}")
