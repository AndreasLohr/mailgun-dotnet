"""Locate where the Stryker JSON payload lives in the HTML report."""
import re
from pathlib import Path

html = Path(
    r"c:\Users\andre\Documents\ClaudeCodeRoot\Libraries\mailgun-dotnet"
    r"\StrykerOutput\2026-05-26.13-23-38\reports\mutation-report.html"
).read_text(encoding="utf-8")

print(f"HTML length: {len(html)}")

# Find every "schemaVersion":"2" occurrence and show 80 chars before each.
for m in re.finditer(r'"schemaVersion":"2"', html):
    i = m.start()
    print(f"\n---@{i}---")
    print(repr(html[max(0, i - 80) : i + 50]))

# Also find every <script> opening tag and show first 120 chars after it.
print("\n=== <script> tag previews ===")
for m in re.finditer(r"<script[^>]*>", html):
    i = m.end()
    print(f"\n@{i}: {repr(html[i:i+200])}")
