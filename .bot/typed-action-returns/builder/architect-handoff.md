# Architect handoff: action handlers returning bare `Data` need typed returns

**From:** builder
**Branch:** `typed-action-returns` (off `runtime2`)
**Source survey:** generated from the trace corpus on `fix-stepvartypes-incremental` (commit `7c54a5b04`). Counts may shift as more goals get built; the *ranking* is what matters.

## The problem

Every step in a goal-build compile gets a "Variables in scope" snapshot in its user prompt:

```
Variables in scope at this step (name → PLang type, derived from prior steps):
%items%(list<string>), %count%(int), %result%(object), %report%(object)
```

`%result%(object)` and `%report%(object)` are the issue. They mean the LLM has zero information about what shape those variables hold — so when a later step asks "assert `%report.content%` contains `'foo'`", the LLM can't tell if `.content` is even a valid property on `%report%`. Result: more LLM guessing, more spurious warnings, more drift.

The variable type comes from the trailing `variable.set Value=%!data%` that captures a producer action's output. The Compile.llm prompt rule says:

> The catalog declares each action's return type via `→ returns T`; use that T as `Value`'s type annotation: `{"name":"Value","value":"%!data%","type":"<T>"}`.

So the LLM is supposed to read `→ returns T` from the action catalog (rendered in the user prompt by `Modules.Describe()` → reflects from C# `Run()`'s `Task<Data<T>>` return). **When the C# returns bare `Task<data.@this>` with no `<T>`, the catalog has nothing to advertise and the LLM correctly falls back to `object`.**

This is the source of most `(object)` noise in the snapshots. It is a real C# typing gap.

## What we want

Each handler in **Category A** below should change its `Run()` signature from `Task<data.@this>` to `Task<data.@this<T>>` for the appropriate `T`, with `SetProp`/value-construction updates to match. The catalog will then advertise `→ returns T` per action, the LLM will emit `type:"T"` on the trailing `variable.set`, and downstream steps see the real type in their snapshot.

## Scope summary

Three categories from the survey. **Architect's planning scope is Category A.**

| Category | Count | Status |
|---|---|---|
| **A**: `Run()` returns bare `Data` (no generic) — C# typing gap | 16 actions / 729 occurrences | **needs design + sweep** |
| B: `Run()` returns `Data<object>` deliberately (genuine polymorphism) | 17 actions / 437 occurrences | correct as-is |
| C: `Run()` returns `Data<T>` with specific T but LLM emits `type:"object"` | 15 actions / 300 occurrences | prompt fix on `fix-stepvartypes-incremental` |

## Category A — the C# typing work

Each row: count of (object)-occurrences attributable to this producer, handler file, suggested return type. Suggestions marked `_(needs domain decision)_` are where the right shape isn't obvious without product context — that's exactly the architect's call.

| Count | Action | Handler | Suggested return type |
|---:|---|---|---|
| 262 | `test.discover` | `PLang/app/modules/test/discover.cs` | `List<File>` — element type already exists at `app.tester.File` |
| 145 | `test.run` | `PLang/app/modules/test/run.cs` | `Results` — already exists at `app.tester.Results` (PLang type `results`) |
| 103 | `file.read` | `PLang/app/modules/file/read.cs` | Polymorphic — text-vs-bytes-vs-parsed. Options: keep `Data<object>` (honest, removes from Cat A); introduce `FileContent` record carrying both shapes; or split into `file.readText` / `file.readBytes`. **Architect decision.** |
| 76 | `test.report` | `PLang/app/modules/test/report.cs` | `string` (the report content path or JSON body) — or new `ReportResult` record. **Architect decision.** |
| 56 | `test.tag` | `PLang/app/modules/test/tag.cs` | `bool` (whether tag was added) — or void-like `Data.Ok()` (which already serializes as Data without T). Marginal value; low priority. |
| 40 | `llm.query` | `PLang/app/modules/llm/query.cs` | Genuinely polymorphic — `Data<object>` is honest. Architect: confirm there's no schema-aware type we want to surface (we have `Schema=...` per call). |
| 18 | `http.request` | `PLang/app/modules/http/request.cs` | `HttpResponse` record (status + headers + body). Probably worth defining. **Architect decision.** |
| 10 | `settings.get` | `PLang/app/modules/settings/get.cs` | Polymorphic by setting key. `Data<object>` defensible. |
| 5 | `http.upload` | `PLang/app/modules/http/upload.cs` | Same `HttpResponse` as `http.request`, or `bool` (uploaded ok). |
| 4 | `goal.call` | `PLang/app/modules/goal/call.cs` | Whatever the called goal returns — genuinely polymorphic, `Data<object>` defensible. |
| 3 | `channel.set` | `PLang/app/modules/channel/set.cs` | `Data.Ok()` (void-like) probably right. |
| 2 | `goal.getTypes` | `PLang/app/modules/goal/getTypes.cs` | `List<Dictionary<string,string>>` (already what it returns; just needs the generic annotation). |
| 2 | `output.ask` | `PLang/app/modules/output/ask.cs` | `string` (the user response). |
| 1 | `builder.types` | `PLang/app/modules/builder/types.cs` | Has a structured shape internally — `BuilderTypes` or `TypeInfo` record. |
| 1 | `builder.actions` | `PLang/app/modules/builder/actions.cs` | `List<ActionInfo>` or equivalent catalog shape. |
| 1 | `builder.goals` | `PLang/app/modules/builder/goals.cs` | `List<Goal>` or `List<GoalInfo>`. |

Plus `mock.intercept` (30 occurrences, handler not located by my regex — coder will know where it lives) — suggested `MockHandle` (already a catalog type).

## Open architectural questions for the planning pass

The two big ones:

1. **`file.read` shape.** Today it returns whatever the file is — string for text, bytes for binary, parsed JSON if `.json`, etc. Choices: (a) `Data<object>` honest polymorphism, type stays `(object)` correctly — moves out of Cat A; (b) introduce a `FileContent` carrier with both `.Text` and `.Bytes` always present; (c) split into `file.readText`/`file.readBytes`/`file.readJson` — most strongly-typed but explodes the action surface. **Architect picks.**

2. **`http.request` / `http.upload` response shape.** These should arguably share a `HttpResponse` record (status, headers, body, duration). Designing once is better than five overlapping ad-hoc returns. **Architect picks the record, then sweep.**

The others (`test.run` → `Results`, `test.discover` → `List<File>`, `goal.getTypes` → its already-correct concrete shape) are mechanical — just need the signature change plus any `SetProp` adjustments. Those can ship without architect involvement once the file.read + http design questions are settled.

## Verification recipe

After each typing change:

```bash
cd /workspace/plang/Tests
rm -rf <relevant>/.build
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build '--build={"cache":false}'
```

Inspect the resulting trace's "Variables in scope" snapshot for a downstream step that uses the variable — should now show `%var%(<T>)` instead of `%var%(object)`. The `.pr`'s trailing `variable.set` should have `type:"<T>"` instead of `type:"object"`.

Across the whole `Tests/` corpus, 729 (object) occurrences in Category A should disappear or move into Category B (the genuinely-polymorphic group) once typed honestly.

## What stays on `fix-stepvartypes-incremental` (not architect's job)

- Category C (LLM ignoring `→ returns T` even when present) — pure prompt fix; builder handles.
- The full categorised survey (all three categories) lives at `.bot/fix-stepvartypes-incremental/builder/coder-handoff-untyped-action-returns.md`. This handoff is the Category A subset rewritten for architect.

## Out of scope for the planning pass

- Don't touch the builder/web prompts — Cat C is mine.
- Don't change action *names* — only return types and any necessary record/type definitions.
- Don't add new PLang-side `Cast`/`Conversion` helpers; the typed returns should flow through `Data<T>` naturally.
