# get-builder-running — restore `plang build` end-to-end

**Branch off:** `module-discovery`. **Merge back into:** `module-discovery` when `plang build` completes a real build. The whole stack above runtime2 is linear (nothing moved after each child branched), so from there every level up to `runtime2` is a fast-forward:

```
runtime2 → scalars-flip-wip → scalars-as-native → compare-redesign-signature-wip → ireader-read-path
  → settings-config-unification → cli-app-property-override → module-discovery → get-builder-running
```

## Goal

`plang build` is broken in a **chain** of failures — each fix unmasks the next. Get it running end-to-end so the module-discovery Stage-4 work (the `.goal`→`.pr` rewire, 4e deletions, parity/sanity) can be validated live. This is build-infrastructure, **not** Stage-4 feature work — hence its own branch.

## 2026-09-25 — `goal-graph-singular` merged in (fast-forward)

The child branch `goal-graph-singular` (about 540 commits, 2026-07-17 → 2026-09-25) is merged in. It was opened to solve layer 4 and did far more: everything singular (graph, wire keys, handler parameters, `os/` goals); goal/step/action/modifier are items; items store no context; the program holds no Data (`action.Property` / `action.Default`); one name → one class in the type registry; the error model (`Error` is the one type, errors shown by `os/system/error/Show.goal` + templates); a loud `plang --test`. Its full record: `.bot/goal-graph-singular/architect/summary.md` and `.bot/goal-graph-singular/coder/open-items.md`.

**The test baseline at the merge: `coder/baseline.md`** — every failing test by name, why, and which item owns it.

## Start here

**First: move the `Tests/` `.pr` files out of `Tests/`** (Ingi, 2026-09-25: allowed; they are stale and misled the bots). All `Tests/**/.build/` content (1719 files in 397 folders) moves to `tools/decider/labels/`, keeping the relative path so each label still maps to its `.goal` source under `Tests/`. The python harness scores against them as labels (`tools/decider/harness.py:123` globs `Tests/**/.build/*.pr`) — update its path. `PLang.Tests/Generator/Builder/CompilePromptTests/DriftCaseArtifactTests.cs` reads one — repoint or retire it. `plang --test` then reports every test as "no .pr", which is true. Tests come back gradually: the builder first, then a few simple tests, then the rest over time (Ingi). Ingi rewrites the CLAUDE.md "do not delete `Tests/**/.build/`" rule himself. The builder's own `.pr` under `os/` do NOT move.

**0. The builder's own `.pr` files are in an old format, so `plang build` cannot load itself.** The readers refuse old keys loudly (`PrFormatOutdated`), never load them empty. `os/system/builder/.build/build.pr:22` still says `"parameter"`. The live set today: `os/system/builder/.build/{app,build,builderchannel,buildgoal,emitbuildevent}.pr` and `os/system/builder/BuildGoal/.build/start.pr` (verify by what boot actually loads).

**Get started with the python builder** (Ingi): `tools/decider/build_pr.py` is the python stand-in for the plang builder — the same pipeline (typesafe decider for stages 1–2; stage 3 through `os/system/builder/llm/Properties.llm` read verbatim, so tuning the prompt there tunes the plang builder too). It builds from the `.goal` SOURCE, writes the current keys (`property` since `c32ac59bd`) and fresh hashes (so #5b goes too), and lists every deviation from the current format. It writes to a staging tree, `tools/decider/out/<folder>/.build/…`, never over the live files. See `tools/decider/README.md` (keys: `TYPESAFE_API_KEY`, `OPENAI_API_KEY`).

1. `python3 tools/decider/build_pr.py os/system/builder` — every `.goal` under the builder (a folder or one file as the argument).
2. Review its deviations and the diff against the live `os/system/builder/**/.build/*.pr`; install the reviewed files over the live ones (Ingi sees every change — no shell rewrite of `.pr` files).
3. `plang build` now loads → verify layer 3 → work down the chain.
4. Once the plang builder runs, let it rebuild its own goals and diff against the python output — same pipeline, same prompt. That is the check for #12 (the builder builds itself).

- Mechanical fallback, no LLM: `tools/decider/pr_bootstrap.py` renames the old keys in existing `.pr` files (and folds a flat inline condition into its child). It keeps whatever the old files had, including stale hashes.
- The same script takes any folder, so `build_pr.py Tests/…` could rebuild the `Tests/` `.pr` before the plang builder runs (#21) — Ingi's call.

The current `.pr` keys (the readers: `goal/serializer/Reader.cs`, `goal/step/serializer/Reader.cs`, `goal/step/action/serializer/Reader.cs`):

| level | current key | refused old key |
|---|---|---|
| goal | `step`, `child`, `tag` | `steps` |
| step | `action` | `actions` |
| action | `name` (not `action`), `property`, `default`, `modifier`, `recovery`, `child` | `parameter`, `parameters` |

A condition's body is its **child** list (`action.Child`); the actions after it in the same step are the else-chain. When hand-building `if X, call Y`, the call goes in the if's `child`, not beside it (a sibling runs when the condition is FALSE).

**Then the chain** (below), from layer 3.

## The chain — state

| # | Layer | State |
|---|---|---|
| 1 | CLI settings bind | ✅ FIXED on module-discovery (`c13532536`). Also: any `list<T>` setting now binds from the CLI (`43cfb6046`, e.g. `--test={"include":[…]}`, `--debug={"variables":[…]}`). |
| 2 | Bootstrap NRE | ✅ FIXED on module-discovery (`a1908911f`) |
| 3 | `Channel '"builder"' not found` — the generated channel lookup read `param.Peek()` (the quoted wire slice) | 🟡 FIX LANDED, NOT VERIFIED LIVE: the generator now reads the parsed value (`PLang.Generators/Emission/Action/this.cs:308-323`, `await __channelParam.Value()`). Verify once step 0 lets the builder load. |
| 4 | `item.Create ⇄ type.Create` bounce | ✅ SOLVED by `goal-graph-singular` — the collection classes are gone; goal/step/action/modifier are items |
| 5 | `error.Handle.Actions.Clr<actions.@this>()` | ✅ GONE — `error.handle` runs `Action.Recovery` (`module/action/error/handle.cs`) |
| 6+ | unknown | each fix unmasks the next |

## Open items that came with the merge

From `.bot/goal-graph-singular/coder/open-items.md`:
- **#12** — the builder has never built itself on the child branch; every builder `.pr` change there was a hand-edit.
- **#21 (rest)** — every `Tests/` `.pr` is old format, so `plang --test` exits 1 ("N tests could not load … rebuild it") until the builder rebuilds them.
- **#22** — the builder-flow change (action descriptions into stage 2, the menu holding actions, notes/examples in stage 3). With Ingi.
- **#5b** — stale hashes in the builder's `.pr` files.
- `os/system/modules/condition/if.examples.md` says "the call is its own action", which reads as a sibling — check it against the child rule above when the builder is revisited.

Parked by Ingi (don't work on them): #18 snapshot (its six SnapshotWire reds), #23 security.

## Discipline (do NOT skip)

- **Baseline = revert + `dev.sh build` + run N×.** `git stash` on a clean/committed tree stashes NOTHING; the test-runner's incremental build may not propagate a production-source revert. One run each side is not a baseline. (See `memory/feedback_baseline_rebuild_discipline.md`.)
- **Clean-rebuild before any `plang --test`/`plang build`** (stale-binary trap).
- Fix at the CAUSE, in C# where possible (runtime takes effect immediately; `.goal` edits need a `.pr` rebuild that this very chain blocks).

## Repro

```bash
cd Tests
BIN=../PlangConsole/bin/Debug/net10.0/plang
# clean rebuild first (stale-binary trap):
# rm -rf ../PlangConsole/bin ../PlangConsole/obj ../PLang/bin ../PLang/obj ... ; dotnet build ../PlangConsole
$BIN build '--build={"files":["BuilderSanity/AddItem.goal"],"cache":false}'
```

## Done =

`plang build '--build={"files":["BuilderSanity/AddItem.goal"]}'` runs to completion (writes a `.pr`, no crash), then merge back into `module-discovery`.
