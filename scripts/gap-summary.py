"""Bucket the spec-vs-SDK gap into actionable categories.

Reads the same data as scripts/openapi-diff.py and prints:
  - INTENTIONALLY EXCLUDED (deprecated per README "Endpoint coverage")
  - ACTIONABLE GAPS — operations the spec defines and the SDK should add
  - METHOD MISMATCHES — SDK calls right path, wrong verb
"""
import re
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from importlib import import_module
m = import_module('openapi-diff')

# Paths the README "Endpoint coverage" section says are intentionally excluded.
# These are deprecated; the SDK directs users to the modern replacements listed.
DEPRECATED_PATTERNS = [
    r'^v3/\{[^}]+\}/events$',                        # /v3/{domain}/events → Analytics.QueryLogsAsync
    r'^v3/stats(/|$)',                               # /v3/stats/* → Analytics.QueryMetricsAsync
    r'^v3/\{[^}]+\}/stats(/|$)',                     # /v3/{domain}/stats/* → Analytics
    r'^v3/\{[^}]+\}/tags?$',                         # /v3/{domain}/tags, /v3/{domain}/tag → AnalyticsTags
    r'^v3/\{[^}]+\}/tag/',                           # /v3/{domain}/tag/* → AnalyticsTags
    r'^v3/\{[^}]+\}/templates(/|$)',                 # /v3/{domain}/templates/* → v4/templates
    r'^v3/forwards(/|$)',                            # /v3/forwards → Routes
    r'^v2/x509(/|$)',                                # /v2/x509 → Mailgun manages TLS auto
    r'^v3/\{[^}]+\}/aggregates/',                    # /v3/{domain}/aggregates/* → Analytics
    # /v3/lists and /v3/lists/{}/members are alternative shapes; SDK uses /pages variants.
    r'^v3/lists$',
    r'^v3/lists/\{[^}]+\}/members$',
]


def is_deprecated(norm_path):
    return any(re.search(pat, norm_path) for pat in DEPRECATED_PATTERNS)


def bucket_actionable(verb, raw_path, op_id):
    """Group actionable missing ops into rough subject areas."""
    p = raw_path
    if '/alerts/' in p or p.endswith('/alerts'):
        return 'Alerts (modern settings API)'
    if 'bounce-classification' in p:
        return 'Bounce Classification (config + stats)'
    if 'dynamic_pools' in p or '/dynamic_pools' in p:
        return 'Dynamic IP Pools (v3 + delegation)'
    if 'subaccounts' in p and ('ip_pool' in p or 'limit' in p):
        return 'Subaccounts (DIPP + limits sub-endpoints)'
    if '/v5/users/me' in p:
        return 'Users (current-user endpoint)'
    if 'limits/tag' in p:
        return 'Domain tag-limits'
    if '/v3/ips/' in p:
        return 'IPs (detailed/bulk)'
    if '/limit/custom' in p:
        return 'Custom Message Limit (modern enable/delete)'
    if '/v5/accounts/features' in p:
        return 'Account features'
    if '/v4/templates' in p:
        return 'Templates v4 (copy/rename/version-copy variants)'
    return 'Other'


def main():
    spec_ops = m.load_spec_operations()
    callsites = m.load_sdk_callsites()
    sdk_keyed = {(c['verb'], c['norm']) for c in callsites if c['verb']}

    deprecated = []
    actionable_by_area = defaultdict(list)

    for (verb, norm), (raw, op_id, summary) in sorted(spec_ops.items()):
        if (verb, norm) in sdk_keyed:
            continue
        if is_deprecated(norm):
            deprecated.append((verb, raw, summary))
        else:
            area = bucket_actionable(verb, raw, op_id)
            actionable_by_area[area].append((verb, raw, summary))

    print(f"=== INTENTIONALLY EXCLUDED (deprecated per README): {len(deprecated)} ===")
    for verb, raw, summary in deprecated:
        print(f"  {verb:7s} {raw:60s}  {summary}")

    total_actionable = sum(len(v) for v in actionable_by_area.values())
    print(f"\n=== ACTIONABLE GAPS: {total_actionable} operations across {len(actionable_by_area)} areas ===")
    for area in sorted(actionable_by_area):
        items = actionable_by_area[area]
        print(f"\n  --- {area} ({len(items)}) ---")
        for verb, raw, summary in items:
            print(f"    {verb:7s} {raw:65s}  {summary[:50]}")


if __name__ == "__main__":
    main()
