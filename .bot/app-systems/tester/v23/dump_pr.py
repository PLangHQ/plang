#!/usr/bin/env python3
"""Dump each test .pr's step text alongside the action+params its build saved.
Read-only. Usage: python3 -I dump_pr.py <testdir> [glob]
"""
import json, sys, glob, os

root = sys.argv[1] if len(sys.argv) > 1 else '.'
pat = sys.argv[2] if len(sys.argv) > 2 else '**/.build/*.test.pr'

def fmt_prop(p):
    n = p.get('name')
    v = p.get('value')
    t = (p.get('type') or {})
    tn = t.get('name')
    tk = t.get('kind')
    ts = tn + (('/'+tk) if tk else '')
    if isinstance(v, (dict, list)):
        v = json.dumps(v, separators=(',', ':'))
    return f"{n}={v!r}:{ts}"

def walk(prpath):
    try:
        d = json.load(open(prpath))
    except Exception as e:
        print(f"!! {prpath}: {e}")
        return
    print(f"\n### {prpath}")
    for s in d.get('step', []):
        txt = s.get('text')
        codes = s.get('code') or []
        parts = []
        for c in codes:
            mod = c.get('module')
            nm = c.get('name')
            props = c.get('property') or []
            ps = ", ".join(fmt_prop(p) for p in props)
            parts.append(f"{mod}.{nm}({ps})")
        print(f"  [{s.get('index')}] {txt!r}")
        for pp in parts:
            print(f"        -> {pp}")

files = sorted(glob.glob(os.path.join(root, pat), recursive=True))
for f in files:
    walk(f)
print(f"\n== {len(files)} pr files ==", file=sys.stderr)
