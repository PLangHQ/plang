#!/usr/bin/env python3
"""
Genericness check — penalize competition candidates whose prompt edits leak
builder-SPECIFIC names/patterns instead of teaching the generic principle.

Rationale (Ingi + repo rule): the builder prompts must teach with placeholders
("call X", "%var%", "the planner"), never hardcode the builder's own goal names,
internal variables, or builder-only modules. A candidate that hardcodes those
would "win" the self-build by MEMORIZING it — and wreck general build quality.
So such edits earn minus points (and past a threshold, disqualification).

This scans the candidate's EDITED prompt files for high-precision builder-specific
tokens. It deliberately ignores ambiguous words (Plan/Compile/Build/Validate/Start)
that legitimately appear as English/phase prose — only distinctive leaks are flagged.

Usage:  genericness.py <file1.llm> [file2.md ...]
Emits one JSON object: {hits:[...], penalty:int, generic:bool}
"""
import json, re, sys

# Distinctive builder GOAL names — safe to flag (not common English / phase words).
# Excluded on purpose: Plan, Compile, Build, Validate, Start (ambiguous prose).
GOAL_NAMES = [
    "BuildGoal", "BuildStep", "BuildSubGoal", "LlmFixer", "RefineActions",
    "FixValidation", "QueryAndVerify", "QueryAndValidatePlan", "EmitBuildEvent",
    "EmitSummary", "BuilderChannel", "ValidateAction", "HandleBuildFailure",
    "HandleStepFailure",
]
# Builder-internal variables — these would NEVER appear in a generic action prompt.
VARS = [
    "planStep", "compileResult", "actionSummary", "goalForLlm", "typeInfo",
    "varTypes", "stepVarTypes", "traceGoals", "compileSystemPrompt", "buildStepPrompt",
    "buildStepUserMsg", "stepForLlm", "actionDetails", "refineMessages", "fixerMessages",
    "fixMessages", "correction",
]
# Builder-only modules/actions (the builder's own plumbing, not user-facing actions).
MODULES = ["builder.", "goal.getTypes"]

PER_HIT = 100  # minus points per leak


def main():
    hits = []
    for path in sys.argv[1:]:
        try:
            lines = open(path, errors="ignore").read().splitlines()
        except Exception:
            continue
        for n, line in enumerate(lines, 1):
            for g in GOAL_NAMES:
                if re.search(rf"\b{re.escape(g)}\b", line):
                    hits.append({"file": path, "line": n, "kind": "goal-name", "token": g, "text": line.strip()[:80]})
            for v in VARS:
                if re.search(rf"%!?{re.escape(v)}\b", line, re.I):
                    hits.append({"file": path, "line": n, "kind": "builder-var", "token": f"%{v}%", "text": line.strip()[:80]})
            for m in MODULES:
                if m in line:
                    hits.append({"file": path, "line": n, "kind": "builder-module", "token": m, "text": line.strip()[:80]})
    print(json.dumps({
        "hits": hits,
        "penalty": -PER_HIT * len(hits),
        "generic": len(hits) == 0,
    }, indent=2))


if __name__ == "__main__":
    main()
