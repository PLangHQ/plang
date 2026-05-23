# Test designer plan — v1 — compile-llm-notes-per-action

Architect plan: `.bot/compile-llm-notes-per-action/architect/plan.md`.
Architect test brief: `.bot/compile-llm-notes-per-action/architect/plan-test-designer.md`.

User approved batches without per-batch review — proceeding straight through all four.

## Test files (all written failing per character contract — `Assert.Fail("Not implemented")` for C#, `- throw "not implemented"` for plang)

### Batch 1 — Drift cases (load-bearing, run 3× fresh-cache)
- `Tests/Builder/CompileLlmNotes/output-write-no-channel.test.goal`
- `Tests/Builder/CompileLlmNotes/assert-equals-no-message.test.goal`

### Batch 2 — Loader (C#)
- `PLang.Tests/App/Modules/CatalogTests/MarkdownTeachingLoaderTests.cs`
  - Notes-only / examples-only / description-only / all-three on action; same on module; missing-file → null.

### Batch 3 — Merge + orphan validation (C#)
- `PLang.Tests/App/Modules/CatalogTests/MarkdownTeachingMergeTests.cs`
  - Both present → both visible on catalog entry (module-level + action-level fields stay split, renderer concats).
  - Only one → other field is null.
  - Empty file → field is null (or empty-and-not-rendered — pin the call coder makes).
- `PLang.Tests/App/Modules/CatalogTests/MarkdownTeachingOrphanTests.cs`
  - Orphan `unknownaction.notes.md` → one warning, build continues, catalog assembled.
  - `module.notes.md` is never an orphan even when no `module.cs` exists.
  - Two orphans → two warnings.

### Batch 4 — Renderer + size + rename guard (C#)
- `PLang.Tests/Builder/CompilePromptTests/StepActionDetailsRenderTests.cs`
  - Notes block rendered for an action in planner set; omitted when text missing; omitted when action not in set.
  - Module + action concat order (module first, blank line, action).
  - Examples block source = markdown after migration; renders only for planner's set.
  - System-prompt size on a `Tests/Simple` step compile drops below 16 KB.
- `PLang.Tests/App/Modules/CatalogTests/CodeAttributeRegressionTests.cs`
  - `[Provider]` no longer exists / `[Code]` exists; PLNG001 diagnostic text references `[Code]`.

## What I am NOT testing (per architect's exclusions)
- Folder rename `*Module/` → `<module>/` itself. The loader tests assume the new layout exists.
- Plan.llm.
- Hot-reload of markdown files.
- Action-handler signatures, `Data<T>` resolution, PLNG001 mechanics (only the rename text).
- Migration-script behavior (it's one-time).

## Verification handshake
The two drift cases are the load-bearing tests. They must pass 3× fresh-cache (per `CLAUDE.md` "Stale-binary trap"). The mechanism tests guard against regressions.
