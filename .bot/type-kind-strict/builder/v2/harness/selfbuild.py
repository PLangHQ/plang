#!/usr/bin/env python3
"""
Self-build scorer: does the builder rebuild ITS OWN goals to the trusted oracle?

Oracle = the committed builder .pr (trusted after the 3 bootstrap fixes). A
self-build "passes" when every builder goal re-maps to the same module.action
per step. Divergence or a crash = the flakiness we're hunting.

Subcommands:
  ref   --root <os/system> --out <reference.json>
        Capture the current (trusted) builder .pr as the oracle.
  score --root <os/system> --ref <reference.json> --traces <dir1> [dir2 ...]
        Compare the just-built builder .pr to the oracle + sum LLM usage across
        the given trace dirs. Emits one JSON object.

Pass criterion per step: produced [module.action, modifiers flattened] (sorted)
== oracle. Mismatches are listed in `divergences` with want/got so we see WHERE
the self-build flips.
"""
import argparse, glob, json, os, sys

# builder .pr that come from a .goal (app.pr is builder.appSave output, not a mapping)
PR_FILES = [
    "builder/.build/build.pr",
    "builder/.build/buildgoal.pr",
    "builder/.build/builderchannel.pr",
    "builder/.build/emitbuildevent.pr",
    "builder/BuildGoal/.build/llmfixer.pr",
    "builder/BuildGoal/.build/plan.pr",
    "builder/BuildGoal/.build/start.pr",
    "builder/BuildGoal/.build/validate.pr",
    "builder/BuildStep/.build/start.pr",
    "builder/BuildStep/.build/validate.pr",
]


def flatten_step(step):
    out = []
    for a in step.get("actions", []) or []:
        out.append(f'{a.get("module")}.{a.get("action")}')
        for m in a.get("modifiers", []) or []:
            out.append(f'{m.get("module")}.{m.get("action")}')
    return sorted(out)


def collect(pr):
    res = {}

    def walk(g):
        res[g.get("name")] = [flatten_step(s) for s in g.get("steps", []) or []]
        for sub in g.get("goals", []) or []:
            walk(sub)

    walk(pr)
    return res


def cmd_ref(args):
    ref = {}
    for rel in PR_FILES:
        p = os.path.join(args.root, rel)
        ref[rel] = collect(json.load(open(p)))
    json.dump(ref, open(args.out, "w"), indent=2)
    n = sum(len(steps) for g in ref.values() for steps in g.values())
    print(f"reference captured: {len(ref)} files, {n} steps -> {args.out}")


def find_usage(o, acc):
    # Real usage blocks carry NUMERIC token counts. The trace also embeds the
    # goal definition, which contains the literal `set %plan.usage% = {model:...,
    # completionTokens:%plan.CompletionTokens%}` template — string-valued, must
    # be excluded. Numeric guard does both jobs.
    if isinstance(o, dict):
        if "model" in o and isinstance(o.get("completionTokens"), (int, float)) \
                and not isinstance(o.get("completionTokens"), bool):
            acc.append(o)
        for v in o.values():
            find_usage(v, acc)
    elif isinstance(o, list):
        for v in o:
            find_usage(v, acc)


def cmd_score(args):
    ref = json.load(open(args.ref))
    total = matched = 0
    divergences = []
    missing = []
    for rel, goals_exp in ref.items():
        p = os.path.join(args.root, rel)
        if not os.path.exists(p):
            missing.append(rel)
            total += sum(len(s) for s in goals_exp.values())
            continue
        try:
            produced = collect(json.load(open(p)))
        except Exception as e:
            divergences.append({"pr": rel, "error": f"unparseable: {e}"})
            total += sum(len(s) for s in goals_exp.values())
            continue
        for gname, exp_steps in goals_exp.items():
            got_steps = produced.get(gname)
            for i, want in enumerate(exp_steps):
                total += 1
                got = got_steps[i] if got_steps and i < len(got_steps) else None
                if got == want:
                    matched += 1
                else:
                    divergences.append({"pr": rel, "goal": gname, "step": i,
                                        "want": want, "got": got})

    acc = []
    for tdir in args.traces:
        for f in glob.glob(os.path.join(tdir, "*.json")):
            if f.endswith("manifest.json"):
                continue
            try:
                find_usage(json.load(open(f)), acc)
            except Exception:
                pass

    print(json.dumps({
        "pass": (matched == total and not missing and not any("error" in d for d in divergences)),
        "matched": matched,
        "total": total,
        "calls": len(acc),
        "completion": sum(u.get("completionTokens", 0) for u in acc),
        "prompt": sum(u.get("promptTokens", 0) for u in acc),
        "cost": round(sum(u.get("cost", 0.0) for u in acc), 6),
        "missingPr": missing,
        "divergences": divergences,
    }))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("ref"); r.add_argument("--root", required=True); r.add_argument("--out", required=True)
    s = sub.add_parser("score"); s.add_argument("--root", required=True); s.add_argument("--ref", required=True); s.add_argument("--traces", nargs="+", required=True)
    a = ap.parse_args()
    (cmd_ref if a.cmd == "ref" else cmd_score)(a)
