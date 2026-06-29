#!/usr/bin/env python3
"""
@octokit/webhooks-schemas の JSON Schema をイベント別に分割するスクリプト。

Usage:
    python3 split_schemas.py <schema_file> <split_dir>
"""

import json
import os
import re
import sys

schema_file = sys.argv[1]
split_dir   = sys.argv[2]

with open(schema_file, encoding="utf-8") as f:
    schema = json.load(f)

defs = schema.get("definitions", {})


def collect_refs(obj, collected, defs):
    """$ref を再帰的に辿り、必要な定義を collected に追加する。"""
    if isinstance(obj, dict):
        ref = obj.get("$ref")
        if ref and ref.startswith("#/definitions/"):
            name = ref[len("#/definitions/"):]
            if name not in collected and name in defs:
                collected[name] = defs[name]
                collect_refs(defs[name], collected, defs)
        for v in obj.values():
            collect_refs(v, collected, defs)
    elif isinstance(obj, list):
        for item in obj:
            collect_refs(item, collected, defs)


# イベント定義を抽出（*$event または *_event キー）
event_keys = [k for k in defs if k.endswith("$event") or (k.endswith("_event") and not k.endswith("$_event"))]
print(f"Found {len(event_keys)} event definitions", flush=True)

generated = []
for event_key in sorted(event_keys):
    # C# クラス名に変換: push$event -> PushEvent, pull_request$event -> PullRequestEvent
    normalized = event_key.replace("$event", "_event")
    class_name = "".join(p.capitalize() for p in normalized.split("_"))
    if not class_name.endswith("Event"):
        class_name += "Event"

    # 依存する定義を収集
    deps = {}
    root_def = defs[event_key]
    collect_refs(root_def, deps, defs)

    # 個別スキーマを構築
    event_schema = {
        "$schema": "http://json-schema.org/draft-07/schema#",
        "title": class_name,
        "definitions": deps,
        **root_def
    }

    out_file = os.path.join(split_dir, f"{class_name}.json")
    with open(out_file, "w", encoding="utf-8") as f:
        json.dump(event_schema, f, indent=2)

    generated.append((class_name, event_key))
    print(f"  {event_key} -> {class_name}.json", flush=True)

print(f"Generated {len(generated)} schema files", flush=True)
