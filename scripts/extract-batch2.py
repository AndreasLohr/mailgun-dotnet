"""Extract operation specs for all v0.9.2 endpoint additions."""
import json
import yaml
from pathlib import Path

SPEC = Path(r"c:\Users\andre\Documents\ClaudeCodeRoot\Libraries\mailgun-dotnet\tmp\mailgun-spec\mailgun.yaml")
with open(SPEC, encoding="utf-8") as f:
    spec = yaml.safe_load(f)

TARGETS = [
    # Dynamic IP Pools v3
    ("get", "/v3/dynamic_pools"),
    ("patch", "/v3/dynamic_pools/{pool_name}"),
    ("post", "/v3/dynamic_pools/all"),
    ("delete", "/v3/dynamic_pools/all"),
    ("post", "/v3/dynamic_pools/{pool_name}/{ip}"),
    ("get", "/v3/domains/dynamic_pools/assignable"),
    ("post", "/v3/domains/all/dynamic_pools/enroll"),
    ("post", "/v3/domains/{name}/dynamic_pools"),
    ("delete", "/v3/domains/{name}/dynamic_pools"),
    ("delete", "/v3/domains/{name}/pool/{ip}"),
    ("get", "/v1/dynamic_pools/domains"),
    ("get", "/v1/dynamic_pools/domains/{name}/history"),
    ("get", "/v1/dynamic_pools/domains/{name}/preview"),
    ("get", "/v1/dynamic_pools/history"),
    ("put", "/v1/dynamic_pools/domains/{name}/override"),
    ("delete", "/v1/dynamic_pools/domains/{name}/override"),
    # Alerts
    ("post", "/v1/alerts/email/test"),
    ("post", "/v1/alerts/slack/test"),
    ("post", "/v1/alerts/webhooks/test"),
    ("post", "/v1/alerts/settings/events"),
    ("put", "/v1/alerts/settings/events/{id}"),
    ("delete", "/v1/alerts/settings/events/{id}"),
    ("put", "/v1/alerts/settings/slack"),
    ("delete", "/v1/alerts/settings/slack"),
    ("put", "/v1/alerts/settings/webhooks/signing_key"),
    ("get", "/v1/alerts/slack/channels/{id}"),
    ("delete", "/v1/alerts/slack/oauth"),
    # Subaccount DIPP
    ("put", "/v5/accounts/subaccounts/{subaccountId}/ip_pool"),
    ("delete", "/v5/accounts/subaccounts/{subaccountId}/ip_pool"),
    ("get", "/v5/accounts/subaccounts/ip_pools/all"),
    ("delete", "/v5/accounts/subaccounts/{subaccount_id}/limit/custom/monthly"),
    # One-offs
    ("put", "/v5/accounts/features"),
    ("get", "/v3/domains/{domain}/limits/tag"),
    ("delete", "/v3/ips/{ip}/domains"),
    ("get", "/v3/ips/details/all"),
    ("put", "/v4/templates/{template_name}/copy"),
    ("put", "/v4/templates/{template_name}/rename/{new_template_name}"),
    ("put", "/v4/templates/{template_name}/versions/{version_name}/copy/{new_version_name}"),
    ("delete", "/v5/accounts/limit/custom/monthly"),
    ("put", "/v5/accounts/limit/custom/enable"),
]


def resolve(ref, spec):
    if not isinstance(ref, str) or not ref.startswith("#/"):
        return ref
    cur = spec
    for part in ref[2:].split("/"):
        cur = cur.get(part, {})
    return cur


def shrink(node, spec, depth=0, max_depth=3):
    if depth > max_depth:
        return "..."
    if isinstance(node, dict):
        if "$ref" in node:
            return shrink(resolve(node["$ref"], spec), spec, depth + 1, max_depth)
        out = {}
        for k, v in node.items():
            if k in ("description", "example", "examples", "x-summary", "x-codeSamples"):
                continue
            out[k] = shrink(v, spec, depth + 1, max_depth)
        return out
    if isinstance(node, list):
        return [shrink(x, spec, depth + 1, max_depth) for x in node[:8]]
    return node


for method, path in TARGETS:
    print("=" * 80)
    print(f"{method.upper():6s} {path}")
    pi = spec.get("paths", {}).get(path)
    if not pi:
        print("  NOT IN SPEC")
        continue
    op = pi.get(method)
    if not op:
        print(f"  No {method}. Available: {[k for k in pi if k in ('get','post','put','patch','delete')]}")
        continue
    print(f"  opId: {op.get('operationId')}  summary: {op.get('summary')}")
    if op.get("parameters"):
        for p in op.get("parameters", []):
            print(f"    param: in={p.get('in')} name={p.get('name')} req={p.get('required',False)} schema={shrink(p.get('schema',{}),spec,0,2)}")
    if "requestBody" in op:
        rb = op["requestBody"]
        content = rb.get("content", {})
        for ct, body in content.items():
            print(f"    body ({ct}): {json.dumps(shrink(body.get('schema',{}),spec,0,4), default=str)[:1200]}")
    if "responses" in op:
        for code, r in op["responses"].items():
            if code in ("200", "201", "204", "default"):
                content = r.get("content", {})
                for ct, body in content.items():
                    if "application/json" in ct:
                        print(f"    response {code}: {json.dumps(shrink(body.get('schema',{}),spec,0,3), default=str)[:600]}")
                        break
                if not content:
                    print(f"    response {code}: (no body)")
