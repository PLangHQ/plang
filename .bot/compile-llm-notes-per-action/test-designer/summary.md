# Test designer — compile-llm-notes-per-action — v1

## Version
v1 — fresh test contract; no prior review.

## What this is
This branch moves per-action LLM teaching out of the global Compile system prompt and out of C# attributes (`[Example]`, `[Description]`) into markdown files at `os/system/modules/<module>/{module,<action>}.{notes,examples,description}.md`. The system prompt drops from ~22 KB to ~15 KB, and each step's user message only carries teaching for actions the planner actually picked.

The architect plan also includes a one-line attribute rename (`[Provider]` → `[Code]`) — the source code already shows `[Code]` everywhere, so I tested only the regression guard against the old name reappearing.

## What was done
Two test surfaces per the architect's brief, plus one rename-guard:

**Plang tests — drift cases (load-bearing, run 3× fresh-cache)**
- `Tests/Builder/CompileLlmNotes/output-write-no-channel.test.goal` — `write out %message%` must produce `formal='output.write(Data=%message%)'`, no `channel=%!data%`.
- `Tests/Builder/CompileLlmNotes/assert-equals-no-message.test.goal` — `assert %message% equals 'hello plang'` must omit `Message` from parameters and from formal; `Expected` matches between them.

**C# tests — mechanism**
- `PLang.Tests/App/Modules/CatalogTests/MarkdownTeachingLoaderTests.cs` (6) — loader reads markdown into catalog; keeps module-level and action-level layers split.
- `PLang.Tests/App/Modules/CatalogTests/MarkdownTeachingMergeTests.cs` (4) — concat order (module first, blank line, action); only-one variants; empty → no block.
- `PLang.Tests/App/Modules/CatalogTests/MarkdownTeachingOrphanTests.cs` (3) — orphan markdown → one warning per orphan via warning channel, no crash. `module.` stem is never an orphan.
- `PLang.Tests/Builder/CompilePromptTests/StepActionDetailsRenderTests.cs` (6) — per-action blocks render only for planner's set; modifiers go through the same path; system prompt for `Tests/Simple` step compile drops below 16 KB.
- `PLang.Tests/App/Modules/CatalogTests/CodeAttributeRegressionTests.cs` (3) — `[Code]` exists, `[Provider]` does not, PLNG001 text references `[Code]`.

All tests are failing-by-construction (`Assert.Fail("Not implemented")` / `throw "not implemented"`) per the test-designer character contract. Coder's job is to make them pass.

## What's still in progress / next
Next bot: **coder**. Drift cases are load-bearing — they must pass repeatedly across 3 fresh-cache builds (CLAUDE.md "Stale-binary trap"). Encode the rebuild incantation when running them.

## Code example — drift-case shape

```plang
TestOutputWriteHasNoSpuriousChannel
/ Drift case 1 — the load-bearing reason this branch exists.
/ Step text `- write out %message%` carries no channel clause. With per-action
/ notes (os/system/modules/output/write.notes.md) carrying the channel routing
/ rule only when output.write is in the planner's set, the LLM should emit:
/   formal='output.write(Data=%message%)'
/   parameters: { Data: %message% }   (no channel key)
/ Pre-fix shape forbidden: formal='output.write(Data=%message%, channel=%!data%)'.
/ Verification: 3 fresh-cache builds in a row.
- throw "not implemented"
```

## Decisions / open items for coder
- Loader tests assume `Notes` and `ModuleNotes` fields on the catalog entry (`app.goals.goal.steps.step.actions.action.@this`). Existing fields `Description`, `ModuleDescription`, `Examples` get repurposed as markdown-sourced after migration. If coder names the new fields differently, the tests are the spec — update field references when wiring tests up.
- Renderer tests target `os/system/builder/templates/v2/stepActionDetails.template`. The system-prompt size bound (≤16 KB) is conservative — the architect estimate is ~15 KB after migration.
- Orphan-tests use the warning channel, not Console.* (per project CLAUDE.md "No Console.* writes in production C#").
