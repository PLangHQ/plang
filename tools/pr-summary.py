#!/usr/bin/env python3
"""
Summarise a built PLang `.pr` file (or every `.pr` under a folder): the step → action
mapping the builder produced, in one terse line per step. Read-only — never edits.

This is the "did the build map correctly?" view you want after every `plang build`
(per building_plang_tests.md "After every build, read the .pr file"). A `.pr` is a
single large JSON blob; this prints just what you check:

    <index> | <step text>  ->  module.action [+mod ...] , module.action ; ...

  - `+mod` marks a modifier action (error.handle / cache.wrap / timeout.after) on the
    host action — so a dropped `on error` / `cache for` shows up as a missing `+mod`.
  - an empty action list prints `(NO ACTIONS)` — the builder's "no actions" failure.

Usage:
    python3 tools/pr-summary.py <path-to.pr | folder>      # one file, or all *.pr under a tree
    python3 tools/pr-summary.py <...> --params             # also show each action's parameters

Examples:
    python3 tools/pr-summary.py Tests/App/CallStack/.build/handledflagsetwhenrecoverysucceeds.test.pr
    python3 tools/pr-summary.py Tests/ScalarsAsNative/Stage1            # every .pr beneath it

For LLM build *traces* (what the planner/compiler saw and returned), use
Documentation/v0.2/inspect-trace.py instead — that's the trace tool; this is the .pr tool.
"""
import argparse
import glob
import json
import os
import sys


def get(d, *names):
    """Case-tolerant key lookup — .pr keys vary (text/Text, actions/Actions)."""
    for n in names:
        if n in d:
            return d[n]
    return None


def fmt_action(a, show_params):
    mod = get(a, "module", "Module") or "?"
    act = get(a, "action", "Action") or "?"
    s = f"{mod}.{act}"
    mods = get(a, "modifiers", "Modifiers") or []
    for m in mods:
        s += f" +{get(m, 'module', 'Module')}.{get(m, 'action', 'Action')}"
    if show_params:
        params = get(a, "parameters", "Parameters") or []
        kv = ", ".join(f"{get(p, 'name', 'Name')}={get(p, 'value', 'Value')}" for p in params)
        if kv:
            s += f"  ({kv})"
    return s


def summarise(path, show_params):
    with open(path) as f:
        data = json.load(f)
    goals = data if isinstance(data, list) else [data]
    print(f"=== {path} ===")
    for g in goals:
        print(f"# {get(g, 'name', 'Name', 'GoalName')}")
        for s in get(g, "steps", "Steps") or []:
            idx = get(s, "index", "Index")
            text = (get(s, "text", "Text") or "").replace("\n", " ")
            actions = get(s, "actions", "Actions") or []
            rhs = " , ".join(fmt_action(a, show_params) for a in actions) if actions else "(NO ACTIONS)"
            print(f"  {idx} | {text}\n      -> {rhs}")


def main():
    ap = argparse.ArgumentParser(description="Summarise a PLang .pr step->action mapping.")
    ap.add_argument("path", help="a .pr file or a folder to scan for *.pr")
    ap.add_argument("--params", action="store_true", help="also show each action's parameters")
    args = ap.parse_args()

    if os.path.isfile(args.path):
        files = [args.path]
    else:
        files = sorted(glob.glob(os.path.join(args.path, "**", "*.pr"), recursive=True))
    if not files:
        sys.exit(f"no .pr files at {args.path}")
    for p in files:
        summarise(p, args.params)


if __name__ == "__main__":
    main()
