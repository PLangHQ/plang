#!/usr/bin/env python3
"""
Builder-prompt eval scorer.

Given a corpus spec (build files + an oracle of expected module.action per step)
and a freshly-built Tests tree + the trace run dir that build produced, report:

  - pass        : did EVERY corpus goal map to its oracle exactly? (the gate)
  - casesPass   : "<matched steps>/<total steps>"
  - calls       : total LLM calls in the run (retries included)
  - completion  : total output tokens   (the cost axis)
  - prompt      : total input tokens
  - cost        : total $ for the run
  - perCase     : per-.pr step-level diffs (for debugging a miss)

"Successful build" == correct mapping, not merely "didn't crash". A step that
maps to the wrong action set fails the gate even if the .pr is valid JSON.

Usage:
  score.py --pr-root <Tests dir> --corpus <corpus.json> --trace-dir <run dir>
Emits one JSON object to stdout.
"""
import argparse, glob, json, os, sys


def flatten_step(step):
    """Sorted list of 'module.action' for a step, modifiers flattened in."""
    out = []
    for a in step.get("actions", []) or []:
        out.append(f'{a.get("module")}.{a.get("action")}')
        for m in a.get("modifiers", []) or []:
            out.append(f'{m.get("module")}.{m.get("action")}')
    return sorted(out)


def collect_steps(pr):
    """Return {goalName: [step,...]} for the root goal and every nested goal."""
    res = {}

    def walk(g):
        res[g.get("name")] = g.get("steps", []) or []
        for sub in g.get("goals", []) or []:
            walk(sub)

    walk(pr)
    return res


def find_usage(o, acc):
    if isinstance(o, dict):
        if "completionTokens" in o and "model" in o:
            acc.append(o)
        for v in o.values():
            find_usage(v, acc)
    elif isinstance(o, list):
        for v in o:
            find_usage(v, acc)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pr-root", required=True)
    ap.add_argument("--corpus", required=True)
    ap.add_argument("--trace-dir", required=True)
    args = ap.parse_args()

    corpus = json.load(open(args.corpus))
    oracle = corpus["oracle"]  # { prPath : { goalName : [[m.a,...], ...] } }

    per_case = {}
    total_steps = 0
    matched_steps = 0
    gate_pass = True

    for pr_rel, goals_expected in oracle.items():
        pr_path = os.path.join(args.pr_root, pr_rel)
        case = {"steps": [], "missing_pr": False}
        if not os.path.exists(pr_path):
            case["missing_pr"] = True
            gate_pass = False
            # count expected steps as failures
            for g, steps in goals_expected.items():
                total_steps += len(steps)
            per_case[pr_rel] = case
            continue
        pr = json.load(open(pr_path))
        produced = collect_steps(pr)
        for gname, exp_steps in goals_expected.items():
            got_steps = produced.get(gname)
            for i, exp in enumerate(exp_steps):
                total_steps += 1
                want = sorted(exp)
                got = flatten_step(got_steps[i]) if got_steps and i < len(got_steps) else None
                ok = got == want
                if ok:
                    matched_steps += 1
                else:
                    gate_pass = False
                    case["steps"].append({"goal": gname, "step": i, "want": want, "got": got})
        per_case[pr_rel] = case

    # usage across the whole run
    acc = []
    for f in glob.glob(os.path.join(args.trace_dir, "*.json")):
        if f.endswith("manifest.json"):
            continue
        try:
            find_usage(json.load(open(f)), acc)
        except Exception:
            pass
    calls = len(acc)
    completion = sum(u.get("completionTokens", 0) for u in acc)
    prompt = sum(u.get("promptTokens", 0) for u in acc)
    cost = sum(u.get("cost", 0.0) for u in acc)

    print(json.dumps({
        "pass": gate_pass,
        "casesPass": f"{matched_steps}/{total_steps}",
        "matched": matched_steps,
        "totalSteps": total_steps,
        "calls": calls,
        "completion": completion,
        "prompt": prompt,
        "cost": round(cost, 6),
        "perCase": {k: v for k, v in per_case.items() if v["steps"] or v["missing_pr"]},
    }))


if __name__ == "__main__":
    main()
