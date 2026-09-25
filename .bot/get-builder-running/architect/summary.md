# architect — get-builder-running

Newest first. The history before 2026-09-25 is in `.bot/goal-graph-singular/architect/summary.md` (merged in here by fast-forward).

**2026-09-25 — `Tests/` `.pr` files moved to `tools/decider/labels/` (`b7b23509b`).** 1719 files in 397 `.build` folders moved as pure renames, keeping their path under `Tests/`; nothing is left under `Tests/`; `os/` untouched. `harness.py` (the only python reader) points at the labels. Retired, not repointed: `DriftCaseArtifactTests` and `BuilderKindStampingTests` (3 green tests). They checked builder output committed beside test goals; against frozen labels they would prove nothing. The six suites match the baseline by name (one known flake). `plang --test`: "324 tests could not load: no .pr", exit 1. Found: `tools/decider/__pycache__/*.pyc` is tracked in git. Next, waiting for Ingi's go: the python builder rebuilds the builder's own `.pr` (`description.md` "Start here").

**2026-09-25 — `goal-graph-singular` merged in (fast-forward `eeb9029b5..20fec8fb8`).** Baseline at the merge: `coder/baseline.md` (168 C# reds, all old; `plang --test` 324 stale). Plan (Ingi): move the test `.pr` out of `Tests/`; the python builder rebuilds the builder's own `.pr`; `plang build` → layer 3 → the chain; tests come back gradually (the builder first, then a few simple tests, then the rest), not through the python builder.
