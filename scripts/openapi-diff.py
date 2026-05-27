"""Diff Mailgun's OpenAPI spec against the SDK's documented route templates.

For every (method, path) pair in the OpenAPI spec:
  - extract it from spec.paths
  - try to find a matching SDK callsite by `routeTemplate:` value + HTTP method.

For every SDK callsite:
  - extract `routeTemplate:` literal + HTTP method
  - check whether that operation exists in the spec.

Produces three lists:
  1. SPEC operations the SDK does NOT cover.
  2. SDK callsites that hit operations NOT in the spec (path or method mismatch).
  3. Path-matched but method-mismatched (e.g. SDK GETs an endpoint the spec only POSTs).
"""

import re
import sys
from collections import defaultdict
from pathlib import Path

import yaml

ROOT = Path(r"c:\Users\andre\Documents\ClaudeCodeRoot\Libraries\mailgun-dotnet")
SPEC_PATH = ROOT / "tmp" / "mailgun-spec" / "mailgun.yaml"

# Map SDK send-method names to HTTP verbs.
SDK_METHOD_TO_VERB = {
    "GetJsonAsync": "GET",
    "GetSkipLimitPageAsync": "GET",
    "GetSkipLimitPageable": "GET",
    "GetJsonByAbsoluteUrlAsync": "GET",
    "PostFormAsync": "POST",
    "PostFormNoResponseAsync": "POST",
    "PostJsonBodyAsync": "POST",
    "PostJsonBodyNoResponseAsync": "POST",
    "PostMultipartAsync": "POST",
    "PostMultipartNoResponseAsync": "POST",
    "PutFormAsync": "PUT",
    "PutFormNoResponseAsync": "PUT",
    "PutJsonBodyAsync": "PUT",
    "PutJsonBodyNoResponseAsync": "PUT",
    "PutMultipartNoResponseAsync": "PUT",
    "PatchMultipartNoResponseAsync": "PATCH",
    "DeleteNoResponseAsync": "DELETE",
    "DeleteMultipartNoResponseAsync": "DELETE",
    "DeleteJsonBodyNoResponseAsync": "DELETE",
    "DeleteJsonAsync": "DELETE",
}

# Pattern: _http.<MethodName>(...) ... routeTemplate: "<template>"
# We scan the routeTemplate first, then walk backwards to find the nearest _http.<Method> call.
ROUTE_TEMPLATE_RE = re.compile(r'routeTemplate:\s*"([^"]+)"')


def normalize_path(path: str) -> str:
    """Normalize a path string for comparison: strip leading slash, replace placeholder
    names with a wildcard so `{name}` and `{domain_name}` and `{domain}` all match."""
    p = path.lstrip("/").strip()
    p = re.sub(r"\{[^}]+\}", "{}", p)
    return p.lower()


def load_spec_operations() -> dict:
    """Return {(method, normalized_path): (raw_path, operation_id, summary)}."""
    with open(SPEC_PATH, "r", encoding="utf-8") as f:
        spec = yaml.safe_load(f)
    ops = {}
    for path, path_item in spec.get("paths", {}).items():
        for method in ("get", "post", "put", "patch", "delete", "head", "options"):
            if method not in path_item:
                continue
            op = path_item[method]
            key = (method.upper(), normalize_path(path))
            ops[key] = (
                path,
                op.get("operationId", ""),
                op.get("summary", "")[:60],
            )
    return ops


def load_sdk_callsites() -> list:
    """Scan src/Mailgun/Services/*.cs for `routeTemplate:` arguments and pair each with
    the nearest preceding `_http.<MethodName>(` call to determine the HTTP verb."""
    callsites = []
    services_dir = ROOT / "src" / "Mailgun" / "Services"
    for cs_file in sorted(services_dir.glob("*.cs")):
        text = cs_file.read_text(encoding="utf-8")
        # Iterate every routeTemplate occurrence; for each, look at the ~25 lines above
        # to find the matching _http call.
        lines = text.split("\n")
        for i, line in enumerate(lines):
            m = ROUTE_TEMPLATE_RE.search(line)
            if not m:
                continue
            template = m.group(1)
            # Walk backwards up to 25 lines to find <Identifier>.<MethodName>(
            # The handle is usually _http but can also be a local variable created by
            # _http.ForSubaccount(...) — e.g. SubaccountsService.DeleteAsync uses an
            # `impersonated` local that wraps the on-behalf-of transport.
            verb = None
            sdk_method = None
            for j in range(i, max(-1, i - 25), -1):
                hm = re.search(r"(?:_http|impersonated)\.(\w+)\s*[<(]", lines[j])
                if hm:
                    sdk_method = hm.group(1)
                    verb = SDK_METHOD_TO_VERB.get(sdk_method)
                    break
            callsites.append({
                "file": cs_file.name,
                "line": i + 1,
                "template": template,
                "norm": normalize_path(template),
                "verb": verb,
                "sdk_method": sdk_method,
            })
    return callsites


def main():
    spec_ops = load_spec_operations()
    callsites = load_sdk_callsites()

    print(f"Spec operations: {len(spec_ops)}")
    print(f"SDK callsites:   {len(callsites)}")

    # SDK -> (verb, norm path)
    sdk_keyed = defaultdict(list)
    for c in callsites:
        if c["verb"] is None:
            continue
        sdk_keyed[(c["verb"], c["norm"])].append(c)

    sdk_paths_only = defaultdict(set)  # norm_path -> set(verbs)
    for v, p in sdk_keyed:
        sdk_paths_only[p].add(v)

    spec_paths_only = defaultdict(set)
    for v, p in spec_ops:
        spec_paths_only[p].add(v)

    # 1. Spec ops missing from SDK
    print("\n=== Spec operations NOT covered by SDK ===")
    missing_in_sdk = []
    for (verb, norm), (raw_path, op_id, summary) in sorted(spec_ops.items()):
        if (verb, norm) not in sdk_keyed:
            missing_in_sdk.append((verb, raw_path, op_id, summary))
    for verb, raw, op_id, summary in missing_in_sdk:
        print(f"  {verb:7s} {raw}  [{op_id}] {summary}")
    print(f"  TOTAL missing: {len(missing_in_sdk)}")

    # 2. SDK callsites missing from spec
    print("\n=== SDK callsites NOT in spec (path+method) ===")
    missing_in_spec = []
    seen = set()
    for c in callsites:
        if c["verb"] is None:
            continue
        key = (c["verb"], c["norm"])
        if key in seen:
            continue
        seen.add(key)
        if key not in spec_ops:
            missing_in_spec.append(c)
    for c in missing_in_spec:
        print(f"  {c['verb']:7s} {c['template']}  ({c['file']}:{c['line']} {c['sdk_method']})")
    print(f"  TOTAL SDK calls that don't match any spec op: {len(missing_in_spec)}")

    # 3. Path matched but method mismatched
    print("\n=== Path matches but method mismatch ===")
    mismatches = []
    for c in missing_in_spec:
        spec_verbs = spec_paths_only.get(c["norm"], set())
        if spec_verbs:
            mismatches.append((c, spec_verbs))
    for c, spec_verbs in mismatches:
        print(f"  SDK does {c['verb']:7s} on {c['template']}; spec only allows {sorted(spec_verbs)}  ({c['file']}:{c['line']})")
    print(f"  TOTAL path-match / method-mismatch: {len(mismatches)}")

    # 4. SDK calls a path that doesn't exist in spec at all
    print("\n=== SDK calls a path NOT in spec at all ===")
    path_not_in_spec = []
    for c in missing_in_spec:
        if c["norm"] not in spec_paths_only:
            path_not_in_spec.append(c)
    for c in path_not_in_spec:
        print(f"  {c['verb']:7s} {c['template']}  ({c['file']}:{c['line']} {c['sdk_method']})")
    print(f"  TOTAL SDK calls hitting paths the spec doesn't define: {len(path_not_in_spec)}")


if __name__ == "__main__":
    main()
