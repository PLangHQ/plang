# Builder handoff — stage 5 (the builder offers kinds) — DONE

Stage 5 is complete, accepted by the architect, and pushed. Latest: `50e83a808`.

## What landed

- **Mechanism** (`0fda873ec`): a per-chosen-type kind follow-up. Stage 2 picks the base type; a `Kind.KindOf` question
  (generated in `pick/list` when the pick is a kind-capable type) offers that type's `kind.list` (less Internal — the
  coder's `OwnInternal` drops the host kinds), or, for a generic, the registry's types as its element; `Decide.goal`
  gains a conditional **stage 3** (`if %questions% contains "@kindof"` → llm.decider + build.pick); `pick.Refine` folds
  the answer into `{name, kind}`. New `decider3.template` (generic — no type/kind names). `decide.pr` rebuilt from the os/
  app root.
- **Teaching** (`84cb5a35a`): the decider is taught by the QUESTION, not descriptions. `variable/set.notes.md` ask
  (json→item, dict only when the step says dict/map; the type itself, kind/element asked after). `file/read.notes.md`
  Type note + ask routing `as <type>` to file.read's Type slot. `decider3.template` wording.
- **Description trim** (`50e83a808`): item/list/duration/dict `type.description.md` describe the type itself (kinds named),
  with the builder's question order and cross-type knowledge removed (that lives in set.notes/decider3).
- **Coder dependencies** (in): `file.read` Type slot `ff2c44069`, Build publishes the as-type `f249fe1cb`, `OwnInternal`.

## Results (final)

5/5, fresh cache:skip: `as decimal`→{number,kind:decimal}, `as list of path`→{list,kind:path}, `as json`→{item,kind:json},
`as iso duration`→{duration,kind:iso}, `read … as text`→text; guards: `set %n%=5`→number, `as text`→text. Plan folder
`test/plan/kind-is-type` 17/17. All six fixtures green + re-pinned: PickListTests 8/8, PickOptionTests 19/19,
ConfirmTemplateTests 2/2, LineTwinTests 1/1, TypePageTests 10/10, ModulePageTests 8/8 (pick_golden, type-/module-reference
goldens re-pinned; diffs read — only the stage-5 teaching delta, no face or unexpected answer changes).

Extra exchange: stage 3 fires only for a goal with a kind-capable coercion; a no-coercion goal renders `{}` and the `if`
guards the call away.

## Build notes

- Rebuild `decide.pr` from the **os/ app root** (`cd os && plang '--build={…,"files":["system/builder/BuildGoal/Decide.goal"]}'`);
  the project root contaminates goal paths with the bin absolute path. `os/` needs the key: `cp .data/setting/data.sqlite
  os/.data/setting/data.sqlite` (gitignored).
- `dev.sh` skipped real rebuilds until `87715faae`; force with `rm -f .devsh-stamp*` before `./dev.sh build` when numbers
  look stale.
- Re-pin: PickList/PickOption via the Explicit `AcceptTheFixture` (Wire binary, `--treenode-filter …/AcceptTheFixture`);
  ModulePage via `AcceptTheGolden`; **TypePageTests has no accept method** — update the `type-reference-generation.md`
  markdown block by hand (the description renders verbatim), then re-run to confirm.

Nothing open. Idle.
