"""
Pull the embedded Stryker JSON payload from the HTML report and break the survivors down
per-file and per-mutator so the triage walk can be ordered by value.
"""
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

REPORT = Path(
    r"c:\Users\andre\Documents\ClaudeCodeRoot\Libraries\mailgun-dotnet"
    r"\StrykerOutput\2026-05-26.13-23-38\reports\mutation-report.html"
)
html = REPORT.read_text(encoding="utf-8")

# Stryker injects the report as `app.report = {...};` inside a <script> tag at body end.
# Find the assignment, then walk forward counting braces (string-aware) to extract the object.
m = re.search(r"app\.report\s*=\s*\{", html)
if not m:
    sys.exit("Could not find `app.report = {` injection point")

start = m.end() - 1  # land on the opening '{'
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
else:
    sys.exit("Could not find matching '}' for report payload")

payload = html[start : end + 1]
print(f"Payload: {len(payload):,} bytes ({(end-start+1)/len(html)*100:.1f}% of HTML)")
report = json.loads(payload)

files = report.get("files", {})
print(f"Files in report: {len(files)}")

by_file_status = defaultdict(Counter)
by_file_mutator_survived = defaultdict(Counter)
by_mutator_overall = Counter()
survivors_by_file = defaultdict(list)
nocoverage_by_file = defaultdict(list)
totals = Counter()

for path, fdata in files.items():
    for mut in fdata.get("mutants", []):
        status = mut.get("status", "?")
        name = mut.get("mutatorName", "?")
        totals[status] += 1
        by_file_status[path][status] += 1
        if status == "Survived":
            by_file_mutator_survived[path][name] += 1
            by_mutator_overall[name] += 1
            survivors_by_file[path].append(mut)
        elif status == "NoCoverage":
            nocoverage_by_file[path].append(mut)

def short(p: str) -> str:
    p = p.replace("\\", "/")
    if "/src/Mailgun" in p:
        return "src/Mailgun" + p.split("/src/Mailgun", 1)[1]
    return p

print("\n--- Totals ---")
for s, n in sorted(totals.items(), key=lambda kv: -kv[1]):
    print(f"  {s:14s} {n}")

# Mutation score and covered-code kill rate
reached = totals["Killed"] + totals["Survived"] + totals.get("Timeout", 0) + totals.get("NoCoverage", 0)
covered = reached - totals.get("NoCoverage", 0)
killed_or_timeout = totals["Killed"] + totals.get("Timeout", 0)
print(f"  Mutation score:  {killed_or_timeout}/{reached} = {killed_or_timeout/reached*100:.1f}%")
print(f"  Covered-code:    {killed_or_timeout}/{covered} = {killed_or_timeout/covered*100:.1f}%")

print("\n--- Top 25 files by Survived count ---")
ranked = sorted(by_file_status.items(), key=lambda kv: -kv[1].get("Survived", 0))
for path, c in ranked[:25]:
    print(
        f"  Surv={c.get('Survived',0):4d}  NoCov={c.get('NoCoverage',0):3d}  "
        f"Killed={c.get('Killed',0):4d}  Timeout={c.get('Timeout',0):2d}   {short(path)}"
    )

print("\n--- Mutator types ranked by survival count ---")
for mut, n in by_mutator_overall.most_common(20):
    print(f"  {mut:35s} {n}")

print("\n--- Files with any NoCoverage survivor ---")
for path, mutants in sorted(nocoverage_by_file.items(), key=lambda kv: -len(kv[1])):
    print(f"  NoCov={len(mutants):3d}   {short(path)}")
