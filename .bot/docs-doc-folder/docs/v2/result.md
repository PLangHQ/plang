# docs v2 — the read of doc/ before merge

## Checked
- Scope: `git diff origin/runtime2...HEAD` touches only `doc/` (19 files) and `.bot/`; nothing else.
- Links: every relative link in `doc/` resolves.
- Ran (branch built): `plang --version` (UnknownSetting), `plang` (GoalNotBuilt), `plang exec` (reads `exec` as a goal), `plang ReadIt.test` (ArgumentRefused), `plang Start.goal`, `plang build` with no flag and no input (NoAppFound); all as `doc/install.md` says.
- Tests the pages cite, re-run: variable/scoping 1, goal 8, loop/foreach 4, condition 30, on/error 14, on/timeout 2, on/cache 2, on/event 3; all pass, counts as stated.
- Databases page: the module list matches `PLang/app/module/`; there is no `db`.
- Not re-run (needs the decider key): the builds in rows 18, 20, 29, 77-79 and the cold-reader runs. They rest on doc2's runs and on `.bot/decider-key/tester/v1/result.md`, which I reproduced for the no-key case on that branch.
- No page contradicts `Documentation/`.

## Fixed in docs/ and README.md (they said what doc/ shows false)
- `plang exec` is no command: `docs/index.md`, `docs/getting-started/hello-world.md`, `how-it-works.md` now say `plang build` then `plang`, and a run never builds.
- `.build/Start/00. Start.pr` → `.build/start.pr` (+ `traces/`): `hello-world.md`, `how-it-works.md`, `folder-structure.md`; the `.pr` example is a trimmed real one (`name`, `step`, `code`, `property`).
- `.db/` → `.data/`, and no database module: `folder-structure.md`.
- `plang --version` is refused: `installation.md` Verify; the release is v0.1.17.3 (Feb 2025) and unproven against these pages: its intro.
- "GPT-4, Claude" → the decider service and its key; "Memory Stack scoped per goal call" → a called goal sees its caller's variables.
- `README.md` linked to `Documentation/README.md`, which does not exist; it now points at `doc/start.md`.

## Left, not contradicted by anything run
- Build cost ($0.002-$0.009 a step) in `how-it-works.md` and `installation.md`; `doc/` marks cost unverified.
- `Setup.goal` runs first (exists in source, `goal/setup/this.cs`).
