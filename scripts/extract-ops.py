"""Dump full operation definitions for a list of (method, path) pairs."""
import json
import sys
import yaml
from pathlib import Path

SPEC = Path(r"c:\Users\andre\Documents\ClaudeCodeRoot\Libraries\mailgun-dotnet\tmp\mailgun-spec\mailgun.yaml")
TARGETS = [
    ("post", "/v3/domains/{domain_name}/messages/{storage_key}"),
    ("put",  "/v4/domains/{domain}/webhooks"),
    ("post", "/v4/domains/{domain}/webhooks"),
    ("delete","/v4/domains/{domain}/webhooks"),
    ("post", "/v1/keys/public"),
    ("delete","/v3/domains/{domain_name}/credentials"),
    ("get",  "/v1/thresholds/hits"),
    ("delete","/v4/templates"),
    ("get",  "/v2/ip_whitelist"),
    ("put",  "/v2/ip_whitelist"),
    ("post", "/v2/ip_whitelist"),
    ("delete","/v2/ip_whitelist"),
    ("delete","/v5/accounts/subaccounts"),
    ("get",  "/v3/ip_pools/{pool_id}/domains"),
]

with open(SPEC, encoding="utf-8") as f:
    spec = yaml.safe_load(f)

def resolve(ref, spec):
    """Resolve $ref like '#/components/schemas/Foo'."""
    if not isinstance(ref, str) or not ref.startswith("#/"):
        return ref
    cur = spec
    for part in ref[2:].split("/"):
        cur = cur.get(part, {})
    return cur

def shrink(node, spec, depth=0, max_depth=4):
    if depth > max_depth: return "..."
    if isinstance(node, dict):
        if "$ref" in node:
            return {"$ref": node["$ref"], "resolved": shrink(resolve(node["$ref"], spec), spec, depth+1, max_depth)}
        out = {}
        for k, v in node.items():
            if k in ("description", "example", "examples", "x-summary"): continue
            out[k] = shrink(v, spec, depth+1, max_depth)
        return out
    if isinstance(node, list):
        return [shrink(x, spec, depth+1, max_depth) for x in node[:5]]
    return node

for method, path in TARGETS:
    print("="*80)
    print(f"{method.upper():6s} {path}")
    path_item = spec.get("paths", {}).get(path)
    if path_item is None:
        print("  NOT FOUND")
        continue
    op = path_item.get(method)
    if op is None:
        print(f"  No {method} on this path. Available: {[k for k in path_item if k in ('get','post','put','patch','delete')]}")
        continue
    print(f"  operationId: {op.get('operationId')}")
    print(f"  summary    : {op.get('summary')}")
    print(f"  parameters :")
    for p in op.get("parameters", []):
        print(f"    - {p.get('in'):6s} {p.get('name')} required={p.get('required',False)}  schema={shrink(p.get('schema',{}), spec, 0, 2)}")
    if "requestBody" in op:
        print(f"  requestBody:")
        print(json.dumps(shrink(op["requestBody"], spec, 0, 5), indent=2, default=str)[:2000])
    print(f"  responses  :")
    for code, r in op.get("responses", {}).items():
        print(f"    {code}: {json.dumps(shrink(r, spec, 0, 4), default=str)[:600]}")
