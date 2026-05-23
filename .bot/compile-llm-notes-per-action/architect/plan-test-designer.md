# Test designer plan — compile-llm-notes-per-action

This branch moves per-action LLM teaching from C# attributes into markdown files at `os/system/modules/<module>/`. Two test surfaces: the catalog-loader mechanism (C# unit), and the end-to-end builder behavior on real steps (plang `--test`).

## Read first

1. **`.bot/compile-llm-notes-per-action/builder/notes-per-action.md`** — the why and the migration map.
2. **`.bot/compile-llm-notes-per-action/architect/plan.md`** — design decisions. Sections most relevant to test design: "Storage layout", "Merge semantics", "Loader", "Validation", "Renderer", "Verification".

## Where to start

**The two end-to-end drift cases.** They are concrete, falsifiable, and pin the whole reason this branch exists. Build them first; the mechanism tests can land in parallel.

## Test surfaces

| Surface | Test kind | Where | Notes |
|---|---|---|---|
| Loader: read markdown into catalog | C# unit | `PLang.Tests/App/Modules/CatalogTests/` (create if absent) | Fixture filesystem with `module.notes.md` + `<action>.notes.md`; assert catalog entry carries both layers. |
| Merge: module first, then action | C# unit | same folder | Both files present → concat with blank line. Only one present → only that text. Neither → block omitted from renderer. |
| Orphan-file validation | C# unit | same folder | `unknownaction.notes.md` under a module folder → one warning surfaced via the warning channel. No crash. Build still succeeds. |
| Renderer: per-action blocks in user message | C# unit or snapshot | `PLang.Tests/Builder/CompilePromptTests/` (locate; likely exists) | When planner's set = `{variable.set}`, rendered prompt contains the Description/Notes/Examples blocks for `variable.set` and *not* for `error.handle` / `loop.foreach` / etc. |
| System prompt size dropped | C# unit or snapshot | same folder | `Compile.llm`-rendered system prompt for a `Tests/Simple` step compiles to < 16 KB (was ~20.8 KB). |
| Drift case 1: `output.write` no spurious channel | plang `--test` | `Tests/Builder/CompileLlmNotes/output-write-no-channel.goal` (new) | Step text `- write out %message%` builds to `formal='output.write(Data=%message%)'`. No `channel=%!data%`. Repeatable across 3 fresh-cache builds. |
| Drift case 2: `assert.equals` no spurious Message | plang `--test` | `Tests/Builder/CompileLlmNotes/assert-equals-no-message.goal` (new) | Step text `- assert %message% equals 'hello plang'` builds to `parameters` with no `Message`, and `formal` matches the parameters (`Expected='hello plang'`, not `%!data%`). |

## Things easy to get wrong

- **Test files live under `Tests/` (uppercase), never `tests/`, `.bot/`, `.build/`, `os/`.** Run via `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`. See `CLAUDE.md` "Running plang Tests".
- **Stale-binary trap.** `plang --test` uses a pre-built `PlangConsole/bin/Debug/net10.0/plang`. Before claiming any pass/fail, rebuild from clean (`rm -rf` the bin/obj trees, `dotnet build PlangConsole`). Phantom failures with shapes like `Action '<module>.<action>' not found` mean stale binary, not a real bug.
- **Do not delete `Tests/**/.build/`.** Those are tracked `.pr` files, not build artefacts.
- **C# test alias clash.** `PLang.Tests/GlobalUsings.cs` aliases `Data`. Do not create `PLang.Tests.App.Data` namespaces — use `*Tests` suffix on folders mirroring `PLang/app/...`. So `PLang.Tests/App/Modules/CatalogTests/`, not `PLang.Tests/App/Modules/Catalog/`.
- **The drift cases must pass repeatedly.** A single green run does not prove the fix — both LLM and cache are non-deterministic. The verification rule (architect's): 3 fresh-cache builds in a row, both cases green. Encode this expectation in the test commentary so reviewers know to re-run.
- **Module-folder casing.** `os/system/modules/` has legacy PascalCase `*Module/` folders. New fixtures and assertions reference lowercase folders (`assert/`, `error/`, etc.) matching `PLang/app/modules/`.

## Conventions to know

- **One concern per test file.** See workflow_test_handoff.
- **No `Console.*` writes.** If a test fixture needs the builder to surface a warning, assert against the warning channel, not `Console.Out`.
- Tests for the loader/merge/orphan surfaces are C# (fast, deterministic). Tests for the drift cases are plang (only the real builder exercises the real prompt).

## What you do NOT need to test

- `Plan.llm` behavior. Untouched in this branch.
- The structural "formal mirrors parameters" rule in the cross-cutting kernel. Already covered by existing tests; not the subject of this work.
- Hot-reload of the markdown files. Explicitly out of scope.
- Action-handler signatures, `Data<T>` resolution, PLNG001. No new handlers in this branch.

## Verification handshake with coder

Your failing tests are the bar. Coder makes them pass. The two end-to-end drift cases are the load-bearing tests — if those pass repeatedly on fresh-cache builds, the structural fix worked. Everything else is mechanism, valuable as regression coverage.
