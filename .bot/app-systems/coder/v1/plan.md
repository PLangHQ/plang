# coder v1 — app-systems: review, then stage 0

Architect's plan: `.bot/app-systems/architect/plan.md` (ready 2026-09-27). The architect (plang-40) asked:
review first, then stage 0; trace before each stage and bring back what doesn't hold.

## Review
- Verify the plan's file:line claims for the next stages (1: Run → Start; 3–4: the type registry and
  the collected type) against the code, and bring back what doesn't hold before building on it.

## Stage 0 — the base
1. Re-record builder-formal's BootstrapTests `Compile` (TypeSafe is back): `GOALS=Compile
   python3 tools/decider/bootstrap.py`, key from /shared/hopkaup/secrets/typesafe.txt for the process
   only; `BootstrapTests` goes green.
2. Baseline of the six C# suites (Modules, Types, Wire, Data, Generator, Runtime) at the branch tip:
   `baseline-tests.md` here, failing names kept for diffs.
3. Delete the deprecated v0.1 files under `os/`: every `NN. stepname.pr` and every `00. Goal.pr`,
   keeping the v0.2 goal `.pr` files (one per goal) and the `.build/` folders.
4. Six suites against the baseline; commit and push.
