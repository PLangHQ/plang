#!/usr/bin/env python3
"""
Inspect a PLang builder trace file (or the latest one in a folder) and surface the
parts most useful for debugging an LLM-driven build.

Usage:
    python3 inspect-trace.py <path-to-trace.json | folder-with-traces>
                             [--what=response|prompt|all]
                             [--filter=GoalName|file.read|...]
                             [--step=N]
                             [--sub-goal=Name]

Examples:
    python3 inspect-trace.py /workspace/plang/Tests/Errors/SimpleRecovery
    python3 inspect-trace.py .../traces --what=response --filter=GoalName
    python3 inspect-trace.py .../639128260427640597_X.json --step=0

Why this exists: `pass1.response` in trace files is post-validateResponse +
post-enrichResponse — NOT the raw LLM API output. To compare raw LLM behaviour,
use `--debug={"llm":{"response":true}}` on the build instead. This tool gives
quick structured views of the post-pipeline state. See `debug.md`.
"""
import argparse
import glob
import json
import os
import sys


def latest_trace(path: str) -> str:
    if os.path.isfile(path):
        return path
    files = sorted(
        glob.glob(os.path.join(path, "*_*.json")),
        key=os.path.getmtime,
        reverse=True,
    )
    files = [f for f in files if "manifest" not in f]
    if not files:
        sys.exit(f"no trace files in {path}")
    return files[0]


def short(value, limit: int = 120) -> str:
    s = json.dumps(value) if not isinstance(value, str) else value
    return s if len(s) <= limit else s[:limit] + "..."


def show_response(resp, args) -> None:
    if not isinstance(resp, dict):
        print("  (no response)")
        return
    for s in resp.get("steps", []) or []:
        idx = s.get("index")
        if args.step is not None and idx != args.step:
            continue
        print(f"  step[{idx}] keep={s.get('keep')} level={s.get('level')} confidence={s.get('confidence')}")
        if s.get("formal"):
            print(f"    formal: {s['formal']}")
        for ai, a in enumerate(s.get("actions", []) or []):
            print(f"    [{ai}] {a.get('module')}.{a.get('action')}")
            for p in a.get("parameters", []) or []:
                if args.filter and args.filter not in p.get("name", "") \
                        and args.filter not in str(p.get("value", "")) \
                        and args.filter not in str(p.get("type", "")):
                    continue
                print(f"         {p.get('name')}=({p.get('type')}): {short(p.get('value'))}")


def show_prompt_section(text: str, needle: str, ctx_before: int, ctx_after: int) -> None:
    idx = text.find(needle)
    if idx < 0:
        print(f"  (needle '{needle}' not found in prompt)")
        return
    print(text[max(0, idx - ctx_before):idx + ctx_after])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("path", help="trace file or folder containing *_*.json traces")
    ap.add_argument("--what", choices=["response", "prompt", "all"], default="all",
                    help="which part to show (default: all)")
    ap.add_argument("--filter", help="filter parameters by name/value/type substring")
    ap.add_argument("--step", type=int, help="filter to a specific step index")
    ap.add_argument("--sub-goal", help="show a specific sub-goal instead of the parent")
    ap.add_argument("--prompt-find", default="Type Information",
                    help="needle string to anchor prompt window (default: 'Type Information')")
    args = ap.parse_args()

    f = latest_trace(args.path)
    print(f"=== trace: {os.path.basename(f)} ===")
    with open(f) as fh:
        data = json.load(fh)

    pass_data = data.get("pass1", {})
    if args.sub_goal:
        for sg_data in data.get("subGoals", []) or []:
            sg = sg_data.get("value") if isinstance(sg_data, dict) else sg_data
            if isinstance(sg, dict) and (sg.get("goal", {}).get("name", "") or
                                         sg.get("goal", {}).get("Name", "")) == args.sub_goal:
                pass_data = sg
                break
        else:
            sys.exit(f"sub-goal '{args.sub_goal}' not in trace")

    if args.what in ("all", "response"):
        print("=== response (post-validate + enrich) ===")
        show_response(pass_data.get("response"), args)

    if args.what in ("all", "prompt"):
        sys = pass_data.get("system", "")
        if sys:
            print(f"=== system prompt window ('{args.prompt_find}') ===")
            show_prompt_section(sys, args.prompt_find, 0, 1500)


if __name__ == "__main__":
    main()
