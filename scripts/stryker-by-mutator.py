"""Dump every Boolean / Equality / Logical survivor across all files (real-bug-yield category)."""
import json
import re
from collections import defaultdict
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

# Mutators where logic bugs hide. NOT string mutations (mostly route-template noise) and NOT
# statement mutations (mostly defensive code path removal — low bug yield).
KEEP = {"Boolean mutation", "Equality mutation", "Logical mutation", "Arithmetic mutation",
        "LogicalNotExpression to un-LogicalNotExpression mutation", "Negate expression",
        "Null coalescing mutation (remove right)", "Null coalescing mutation (remove left)",
        "Null coalescing mutation (left to right)",
        "Linq method mutation (Last() to First())", "Linq method mutation (Max() to Min())",
        "Linq method mutation (FirstOrDefault() to First())",
        "Conditional (true) mutation", "Conditional (false) mutation"}

per_file = defaultdict(list)
for path, fdata in report.get("files", {}).items():
    source_lines = fdata.get("source", "").split("\n")
    for mut in fdata.get("mutants", []):
        if mut.get("status") not in {"Survived", "NoCoverage"}:
            continue
        if mut.get("mutatorName") not in KEEP:
            continue
        loc = mut.get("location", {})
        line = loc.get("start", {}).get("line", 0)
        src = source_lines[line - 1].strip() if 0 < line <= len(source_lines) else ""
        per_file[path].append((mut["status"], line, mut["mutatorName"], src[:90], mut.get("replacement", "")[:60]))

def short(p):
    p = p.replace("\\", "/")
    return "src/Mailgun" + p.split("/src/Mailgun", 1)[1] if "/src/Mailgun" in p else p

total = 0
for path in sorted(per_file, key=lambda p: -len(per_file[p])):
    entries = per_file[path]
    total += len(entries)
    print(f"\n=== {short(path)}  ({len(entries)}) ===")
    for status, line, mut, src, repl in entries:
        print(f"  [{status[:4]}] L{line:>3}  {mut[:30]:30s}  {src}")
        print(f"          replacement: {repl}")

print(f"\nTOTAL logic-mutator survivors: {total}")
