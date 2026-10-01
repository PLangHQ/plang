# builder — summary

## Version
v1 (2026-10-01)

## What this is

Ingi pulled `app-systems` and asked builder to get familiar with the rearchitected builder
and refresh the docs, which had drifted badly. The builder changed from a two-phase
**Plan → Compile** pipeline to a three-stage **Decide → Properties → match** pipeline, and
the `.pr` format changed too — but every builder-architecture doc still described the dead
design. This version documents the new builder and relocates the docs next to the code.

## What was done

### Understood the new builder (verified from source, not the stale docs)
- **Pipeline** (`os/system/builder/BuildGoal/Start.goal` → `Compile`): **Decide** (two
  `llm.decider` exchanges — closed-set questions naming which actions each step may use;
  `build.pick` writes `step.Pick`) → **Properties** (one `llm.query` over the whole goal;
  the LLM writes each step's actions in *formal* `[i] module.action(Name=value); …`) →
  **match** (`build.match` parses the formal into each step's `code`, no LLM). Then
  `build.fold` re-parents indented sub-steps under their condition.
- **Recovery goals**: `UnwrittenNumber`→`ConfirmNumbers`, `ElseWithoutIf`→`SourceError`,
  other→`FixSteps`; goal-level→`HandleBuildFailure`. (Replaces `LlmFixer`/`RefineActions`/
  `FixValidation`.)
- **C#**: `build.{load,goals,pick,match,fold,goalsSave,unreached,appSave}` →
  `PLang/app/module/build/code/Default.cs` (IBuilder); `llm.decider` → `decider.cs` +
  `IDecider`/`TypeSafe` (posts to the "systemone" endpoint); `step.Pick` type at
  `PLang/app/goal/step/pick/list/this.cs`.
- **`.pr` format**: `step[]` (not `steps`), `code[]` (not `actions`), `property[]` (not
  `parameters`) with typed `type:{name,kind?,template?}` and parsed `variable:[]`; a
  condition's body nests in `code[].child[]`; sub-goals in top-level `child[]`. No `formal`,
  no `modifiers` array (clauses are peer `on.*` code entries), no `description`/`version`.
- **Teaching**: action prose is lazy file items on the catalog element
  (`PLang/app/goal/step/action/this.Schema.cs`: Description/Note/Examples) and the module
  element (`PLang/app/module/this.cs`: Description/Notes), read per build; **no
  `MarkdownTeaching.cs` / `ScanOrphans`** (deleted). `examples.md` is now `Step text:` /
  `Properties:` JSON (was `Mapping:` + `|` formal).

### Docs: rewritten and co-located as `.code.md` (Ingi's call)
Old `Documentation/v0.2` docs removed; new homes next to the code they describe:

| New file | Replaces | By |
|---|---|---|
| `os/system/builder/start.md` | (new index Ingi asked for) | builder |
| `os/system/builder/builder.code.md` | understanding-the-builder.md | builder |
| `os/system/builder/pr-format.code.md` | build_process.md | builder |
| `os/system/builder/bootstrap.code.md` | building-the-builder.md | fork |
| `os/system/builder/debugging.code.md` | debugging-builder-failures.md | fork |
| `os/system/modules/catalog.md` | action-catalog.md | builder |
| `Documentation/v0.2/build.md` (rewritten in place) | — (CLI, stays) | builder |

- `catalog.md` is a plain `.md`, **not** `.code.md` — Ingi's rule: a `.code.md` companions
  a real code file (`Decide.code.md` ↔ `Decide.goal`), and there is no `catalog.cs`.
- Repointed every inbound cross-link (`architecture.md`, `building_plang_tests.md`,
  `todos.md` ×2, `docs/modules/builder.md`, `build.md`). Verified all `.md` links resolve.
- Filed a CLAUDE.md proposal (`.bot/app-systems/claude-md-proposals.md`) for the stale
  line-22 pipeline wording + the moved catalog path (CLAUDE.md is docs-owned).

### Ingi's other directives
- **Memory**: saved the "other bots contact builder with builder bugs / mapping intent;
  builder makes the builder able to map it the plang way" role, plus self-improvement
  lessons from this session (see memory dir).
- **Architect request**: filed `.bot/app-systems/builder/to-architect.md` — (1) builder
  takes over coder's builder-building work incl. the C#; (2) proposed retirement of the
  Python decider validation (`tools/decider/`). No live architect session was reachable to
  message directly.

## Code example

The pipeline's heart (`BuildGoal/Start.goal`, the `Compile` goal):

```plang
Compile
- if %goal.Step.IsCached%, return
- condition.if(Left=%goal.Step.IsAnswered%) { build.match(Goal=%goal%, Answer=""); goal.return() }
- call Decide
- call Properties
- build.match(Goal=%goal%, Answer=%answer%); on.error(Key="UnwrittenNumber", Recovery=[goal.call(Name="ConfirmNumbers")]); on.error(Key="ElseWithoutIf", Recovery=[goal.call(Name="SourceError")]); on.error(Recovery=[goal.call(Name="FixSteps")])
```

## What's next / flagged
- **Architect to decide** the two ownership items in `to-architect.md` before builder acts
  on taking over the C# or retiring `tools/decider/`.
- **Incidental staleness not in scope this pass**: `debug.md`, `builder-runtime.md`,
  `trace.md`, `building_plang_tests.md` still carry minor old-pipeline mentions
  (planner/compiler). Flagged, not fixed — a future doc pass.
- **Not verified live**: the bootstrap rebuild recipe's exact invocation (needs an LLM
  endpoint); documented from the current file set + the preserved cwd/path-qualify wisdom.
