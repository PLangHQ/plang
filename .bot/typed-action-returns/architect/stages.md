# Stages — `typed-action-returns`

**From:** architect
**Companion to:** `architect/plan.md` (v3.4)
**For:** test-designer (writes failing-test contract per stage), then coder (implements until tests pass)

This document breaks Branch A (`typed-action-returns`) into five stages. Each stage is self-contained, sequenceable, and small enough to test-design + implement + auditor-pass on its own. Branch B (`test-report-typed-object`) is a separate fork off `runtime2` and is staged separately at the bottom — not included in this branch's pipeline.

## Dependency graph

```
              Stage 0
        (foundation infra)
                │
        ┌───────┴───────┐
        ▼               ▼
    Stage 1         (parallel-OK)
   (Test rename)
        │
        ▼
    Stage 2
   (mechanical
    typings)
        │
        ▼
    Stage 3                    Stage 0 also unblocks Stage 4 directly,
   (HTTP Response              but Stage 4 depends on Stage 3's Response
    + content-type             only via http.*.Build() — keep sequential
    dispatch)                  to avoid merge conflicts.
        │
        ▼
    Stage 4
   (per-action Build()
    + (type) hint
    + multi-segment ext)
```

Stage 0 ships infrastructure; Stages 1–4 ship user-visible behaviour built on it. Stage 1 (rename) can land in parallel with Stage 0 if a coder prefers — they touch disjoint code. Stages 2 → 3 → 4 should land in order to keep `.pr` snapshots stable.

---

## Stage 0 — Foundation infrastructure

**Goal.** Land the new primitives the rest of the work depends on, without changing any action's visible behaviour.

**Scope (in):**

1. **Remove `[PlangType]` attribute from the codebase.** Source generator derives PLang type name from class name (lowercased). Existing usages (e.g. `[PlangType("testfile")]` on `app/tester/File.cs`, `[PlangType("results")]` on `app/tester/Results.cs`, any others) are removed; the attribute class itself is deleted. Verify the type registry (`app/types/this.cs Primitives`) still maps correctly via derivation.
2. **`Build()` method on `IClass`.** Add an optional `Task<Data> Build()` method to the action handler interface. Default implementation returns `Task.FromResult(Data.Ok())` (i.e. handlers that don't override contribute nothing). Source generator wires it like any other method on the action record.
3. **`builder.validate` plumbing for Build().** During the validate pass, iterate each step's actions; for each action call `Build()`; collect the results. If `Build()` returns `Data.Ok(typeName)`, set `typeName` as the Type on the step's terminal `variable.set`. If it returns `Data.Fail(...)`, surface the error and fail validation. If it returns plain `Data.Ok()` (no value), do nothing.
4. **Named channels — `Channel("name")` lookup with no-op fallback.** Extend the channels infrastructure to support build-scoped named channels in addition to the existing `Output` / `Input` / `Debug`. New API: `Channel(string name)` returns the named channel if registered with the current actor; otherwise returns a no-op sink (`/dev/null` semantics) so writes succeed silently. The builder creates a `"builder"` channel when a build starts and disposes it when the build ends.
5. **`BuildWarning` record.** Define `public sealed record BuildWarning(IClass Action, string Message);` at an appropriate location (likely `app/builder/BuildWarning/this.cs` per OBP-singular folder). Used as the payload written to `Channel("builder")`.
6. **Data materialization owned by Data.** `Data` exposes materialization via its own `.Type` — caller never picks via a generic. Concretely: property access (`%var.foo%`) and any C# accessor go through a Data method (`Materialize()` / internal `Value` getter / coder's choice) that uses `.Type` to look up the materializer from the `Serializers` registry, runs it once, caches the result. Add `Data.As(string typeName)` for explicit cross-type coercion (rare). **Remove any generic `Data.As<T>()` if it exists.**

**Scope (out):**
- No action handler's `Run()` signature changes in this stage.
- No per-action `Build()` overrides ship in this stage — just the interface + plumbing.
- Multi-segment `GetByExtension` deferred to Stage 4.

**Touch points:**
- `PLang/app/modules/<*>/this.cs` (or wherever `IClass` is defined)
- `PLang/app/types/this.cs` (PlangType derivation, materializer dispatch)
- `PLang/Generators/` (source generator: derive PLang type name; wire Build())
- `PLang/app/channels/` (named channel lookup, no-op fallback)
- `system/builder/validate.goal` or the C# equivalent of `builder.validate` (per-action Build() iteration)
- `PLang/app/builder/BuildWarning/this.cs` (new file)
- Any file currently carrying `[PlangType("...")]` attribute usage (remove the attribute, rely on derivation)

**Test-designer prompt for Stage 0:**

Write a failing-tests contract covering:
1. **C# unit (PLang.Tests):**
   - `[PlangType]` attribute does not exist (compilation fails if any source file references it).
   - PLang type name for `app.tester.Results` derives correctly to `"results"` via source generator.
   - `IClass.Build()` default returns `Data.Ok()`; a hand-written test action overriding `Build()` to return `Data.Ok("foo")` is reachable through the validate pass and the terminal variable.set Type is set to `"foo"`.
   - `Channel("nonexistent")` returns a sink that accepts writes without throwing.
   - `Channel("builder")` exists during a build and is disposed after.
   - Writing a `BuildWarning` to `Channel("builder")` and subscribing to it receives the payload with the expected `Action` and `Message`.
   - `Data.As("json")` on a string-valued Data with `.Type="json"` returns a `JsonNode`.
   - `Data.As<T>()` (generic) does **not** exist as a public API.
2. **PLang `.test.goal` (under `Tests/`):**
   - Trivial build with a no-op handler still works (regression — no behaviour change).
   - Build a goal where a hand-written test action overrides `Build()` and emits a typed terminal; the resulting `.pr`'s trailing `variable.set` carries the right Type. Snapshot the `.pr`.

**Coder prompt for Stage 0:**

Implement the six items in **Scope (in)** until the failing-tests contract goes green. Pay particular attention to:
- The source generator change for `[PlangType]` derivation — there may be existing types where the attribute's argument differed from the class name in non-trivial ways; surface those as build errors and resolve case-by-case.
- The named-channel no-op fallback must be cheap (per-write zero allocations) — channel writes happen on hot paths.
- `Data.As(string typeName)` and the property-access path through Data both go via the same materializer lookup; don't duplicate the logic. Cache the materialized value once.

**Acceptance:**
- All C# tests green.
- All existing PLang tests still pass (no behaviour change).
- `[PlangType]` cannot be imported anywhere in PLang sources.
- A build that uses a test handler overriding `Build()` produces a `.pr` with the expected Type on the terminal variable.set.

---

## Stage 1 — `tester/File` → `tester/Test` rename

**Goal.** Rename the test-domain entity. Drops one `[PlangType]` site (legitimate-looking but actually still dropped per Stage 0).

**Scope (in):**

1. Move `PLang/app/tester/File.cs` → `PLang/app/tester/Test/this.cs`. Rename class `File` → `Test`. PLang type name derives to `"test"`.
2. Update consumers — `test.discover` (return shape), `test.run` (input shape), `test.report` (consumer), and any PLang .test.goal files referencing `tester.File`.
3. Drop any remaining `[PlangType("testfile")]` usage (already removed in Stage 0 if that landed first, but verify).

**Scope (out):**
- Don't change `test.discover.Run()` signature beyond the type rename — that's Stage 2.

**Touch points:**
- `PLang/app/tester/File.cs` → `PLang/app/tester/Test/this.cs`
- `PLang/app/modules/test/discover.cs`, `run.cs`, `report.cs`
- C# unit tests under `PLang.Tests/App/TesterTests/` (or similar)
- PLang `.test.goal` files under `Tests/TestModule/` that reference `.test/` discover output

**Test-designer prompt for Stage 1:**
- C# unit: `app.tester.File` does not compile; `app.tester.Test` exists with the 9 fields.
- C# unit: `test.discover` returns a collection of `Test` (type-only check; signature change is Stage 2).
- PLang `.test.goal`: existing test-runner pipeline (`test.discover` → `test.run` → `test.report`) still works end-to-end.

**Coder prompt for Stage 1:**

Straight rename. Use IDE rename if possible to catch all references; manually scan for string-form references (in goal files, in comments, in test names).

**Acceptance:**
- All tests green.
- Grep for `tester.File` and `\"testfile\"` returns zero hits across PLang sources and Tests/.

---

## Stage 2 — Mechanical typings (10 handlers)

**Goal.** Type the 10 handlers in `plan.md` A.1 whose return shape is mechanically obvious. Each handler's `Run()` signature changes from `Task<data.@this>` to `Task<data.@this<T>>`.

**Scope (in):**

| Action | T |
|---|---|
| `test.discover` | `List<Test>` |
| `test.run` | `Results` |
| `goal.getTypes` | new strongly-typed record (coder reads current return; likely `List<TypeInfo>` at `app/builder/Types/TypeInfo/this.cs` or reuses existing model) |
| `output.ask` | `string` |
| `channel.set` | `Data` (void-like — no T) |
| `mock.intercept` | `Mock` (new record at `app/mock/Mock/this.cs`) |
| `builder.types` | new record at `app/builder/Types/this.cs` |
| `builder.actions` | new record at `app/builder/Actions/this.cs` |
| `builder.goals` | new record at `app/builder/Goals/this.cs` |
| `test.tag` | `bool` (low priority — defer unless one-liner) |

**Scope (out):**
- `file.read`, `http.request`, `http.upload`, `llm.query`, `goal.call`, `settings.get` — stay `Data<object>` at C# (Stages 3 and 4 handle their inference path).

**Touch points:**
- `PLang/app/modules/<module>/<action>.cs` for each row above
- New record files for builder.types/actions/goals/Types/TypeInfo and mock.Mock

**Test-designer prompt for Stage 2:**
- C# unit per handler: `Run()` returns `Task<Data<T>>` with the right T; `Data.Value` is the right concrete type after a successful call.
- PLang `.test.goal` per handler: build a step that produces a value via this action, capture the trace's "Variables in scope" snapshot for a downstream step, assert the variable's type annotation shows `<T>` not `(object)`.

**Coder prompt for Stage 2:**

For each handler:
1. Change `Run()` signature.
2. Update value construction at the bottom of `Run()` to use `Data.@this<T>.Ok(value)` (mindful of the Data<object> double-wrap footgun documented in CLAUDE.md).
3. For new records (builder.*, mock.Mock, TypeInfo): create the OBP-singular folder and `this.cs`.

**Acceptance:**
- All C# tests green.
- All PLang `.test.goal` runs green.
- Snapshot the per-handler trace; the (object) → (T) annotation flip is visible in the snapshot for each.

---

## Stage 3 — HTTP `Response` record + Content-Type dispatch

**Goal.** Type the HTTP module's return shape; dispatch response body by Content-Type at runtime.

**Scope (in):**

1. **`Response` record** at `app/http/Response/this.cs`:
   ```csharp
   public sealed record Response(
       int Status,
       Dictionary<string, string> Headers,
       object? Body,
       TimeSpan Duration);
   ```
2. **`http.request` / `http.upload` typed signatures:** `Task<data.@this<Response>>`.
3. **Runtime Content-Type dispatch on Body:**
   - Parse `Content-Type` from response headers.
   - Look up serializer via `Serializers.GetByContentType(ct)`.
   - Deserialize body using the serializer.
   - Unknown / no `Content-Type` → `byte[]`. `text/*` with unknown subtype → `string`.
4. **`http.download` unchanged** (saves to file, doesn't return parsed content).

**Scope (out):**
- `http.request.Build()` and `http.upload.Build()` (URL extension → terminal type) are Stage 4. Stage 3 just lands the typed return + runtime parsing.

**Touch points:**
- `PLang/app/modules/http/request.cs`, `upload.cs`, `download.cs`
- `PLang/app/modules/http/code/IHttp.cs`, `code/Default.cs` (the provider impl that calls the wire)
- `PLang/app/http/Response/this.cs` (new)
- `PLang/app/channels/serializers/this.cs` (if Content-Type dispatch needs registry tweaks)

**Test-designer prompt for Stage 3:**
- C# unit: `Response` record carries all four fields correctly.
- C# unit: Content-Type dispatch — `application/json` → `Body` is `JsonNode`; `text/html` → `string`; `image/png` → `byte[]`; missing Content-Type → `byte[]`; `text/csv` → `Csv` (if a Csv materializer exists).
- C# unit: `http.request.Run()` returns `Task<Data<Response>>`.
- PLang `.test.goal`: hit a mock endpoint (or use `mock.intercept` to stub the response), assert `%resp.Status%`, `%resp.Headers.Content-Type%`, `%resp.Body.foo%` work as expected for a JSON response.

**Coder prompt for Stage 3:**

The provider-side code (`IHttp.SendAsync` / `Default.cs`) already gets the wire response. The shape change is wrapping that into a `Response` record and dispatching the body. Keep `http.download` untouched.

**Acceptance:**
- All C# + PLang tests green.
- A goal that calls `http.request` to a JSON endpoint can read `%resp.Body.fieldName%` directly without an explicit parse step.

---

## Stage 4 — Per-action `Build()` + `(type)` hint + multi-segment extension

**Goal.** Land the compile-time inference layer — the part that makes the snapshot stop saying `(object)` for the inferrable-but-polymorphic actions.

**Scope (in):**

1. **`file.read.Build()`** — if `Path.Value` is a string literal, parse extension via `Path.GetExtension`, look up MIME via `Context.App.Formats.Mime(ext)`, map to PLang type via `data.type.FromMime(mime)`, return `Data.Ok(typeName)`. If the literal file doesn't exist on disk, write a `BuildWarning` to `Channel("builder")`. For non-literal paths, return `Data.Ok()`.
2. **`llm.query.Build()`** — if `Schema.Value` is non-empty, return `Data.Ok("json")`. Else if `Format.Value` is non-empty, return `Data.Ok(Format.Value)`. Else `Data.Ok()`.
3. **`http.request.Build()` / `http.upload.Build()`** — if URL arg is a literal with a recognized extension (e.g. `https://api/x.json`), return `Data.Ok(<type>)`. Else `Data.Ok()`. (Distinct from runtime Content-Type dispatch in Stage 3 — this is the compile-time hint.)
4. **`(type)` hint Compile.llm rule.** Add one rule to the Compile prompt's cross-cutting kernel teaching:

   > When a variable reference or string literal is followed by `(type)` in a write-target position (e.g. `write to %answer%(json)`), use that `type` as the Type on the step's terminal `variable.set`. PLang's parser does not interpret `(type)` — it's prose the LLM reads. Unknown type names are passed through; the runtime materializer will surface a clear error on first access if the type is unsupported.

5. **Multi-segment `GetByExtension`** on the serializer registry — walks file extensions across multiple segments (`.junit.xml`). One-line registry extension. (Needed by Branch B's JunitSerializer but lands here because it's a generic registry improvement.)
6. **User `(type)` hint wins over `Build()` inference.** If the LLM already set a Type on the terminal variable.set via the hint rule, the validate pass does not overwrite it with Build()'s inferred type.

**Scope (out):**
- `output.ask.Build()` returns `Data.Ok()` always — defaults handled by the prompt rule.
- `goal.call` and `settings.get` are intentionally not given Build() implementations.

**Touch points:**
- `PLang/app/modules/file/read.cs`
- `PLang/app/modules/llm/query.cs`
- `PLang/app/modules/http/request.cs`, `upload.cs`
- `system/builder/Compile.llm` (or the markdown teaching equivalent)
- `PLang/app/channels/serializers/this.cs` (multi-segment GetByExtension)
- `PLang/app/modules/builder/validate.cs` (precedence rule: user hint wins over Build())

**Test-designer prompt for Stage 4:**
- C# unit: `file.read.Build()` returns `Data.Ok("csv")` for `Path="foo.csv"`; returns `Data.Ok()` for `Path="%p%"`; writes a `BuildWarning` to `Channel("builder")` for a non-existent literal path.
- C# unit: `llm.query.Build()` returns `Data.Ok("json")` when Schema set; returns `Data.Ok(Format.Value)` when Format set without Schema; returns `Data.Ok()` otherwise.
- C# unit: `http.request.Build()` returns the right type for a literal `.json` URL.
- C# unit: `Serializers.GetByExtension(".junit.xml")` resolves (will need a stub serializer registered for the test).
- PLang `.test.goal`:
  - `file.read 'foo.csv', write to %x%` — assert downstream `%x%(csv)` in trace snapshot.
  - `llm.query schema=..., write to %r%` — assert `%r%(json)`.
  - `output.ask 'q', write to %a%(json)` — assert `%a%(json)` (hint precedence).
  - `file.read 'missing.csv'` — assert build trace contains a warning with the expected message.

**Coder prompt for Stage 4:**

Build() implementations are short. The harder piece is the validate-pass precedence: user hint > Build() inference > fallback to `object`. Make sure the validator can tell the difference between "LLM emitted Type=json explicitly" and "LLM emitted Type=object by default" — Build() should only fill in when the LLM didn't already specify.

**Acceptance:**
- All C# + PLang tests green.
- Trace snapshot for the `Tests/` corpus shows the (object) → (csv) / (json) / etc. flip on the inferrable-but-polymorphic actions.
- `file.read` on a missing literal produces a warning, not an error; the build proceeds.

---

## Branch B — `test-report-typed-object`

Separate fork of `runtime2`, separate test-designer + coder pipeline. **Not part of `typed-action-returns`'s sequence.** Stages outlined briefly so it's not forgotten:

- **Stage B.0** — Depends on Branch A having landed (or merged in). Picks up `Build()`, named channels, `[PlangType]` removal.
- **Stage B.1** — `Report` record at `app/tester/Report/this.cs`. `Run` already carries everything needed for `Report.Runs`.
- **Stage B.2** — Refactored `test.report` to return `Data<Report>`; remove `BuildJson`, `BuildJUnit`, `System.IO`, `Format` param.
- **Stage B.3** — `JunitSerializer` at `app/channels/serializers/serializer/Junit.cs`; dispatched via the multi-segment `GetByExtension` from Branch A's Stage 4.
- **Stage B.4** — Calling-site change in `os/system/test.goal`: explicit `file.save %report%` lines for `.json` and `.junit.xml`.
- **Stage B.5** — Tests: C# unit, JSON snapshot, PLang `.test.goal`.

Architect produces a separate `stages.md` on the `test-report-typed-object` branch when its turn comes; this is just a reminder it's queued.

---

## Cross-stage rules

- **No `.build/` deletion.** Per CLAUDE.md: those are tracked `.pr` files, not artefacts. Coder regenerates via `plang build`, never via `rm -rf .build/`.
- **PLang test discovery:** `cd Tests/ && ../PlangConsole/bin/Debug/net10.0/plang --test`. Never run `plang --test` from the project root (catches stale `.bot/` test goals).
- **Stale-binary trap:** before claiming any PLang test result, rebuild from clean per the recipe in CLAUDE.md. Phantom `Action 'X.Y' not found` or `(null)` reads of valid `%!<infra>%` symbols mean stale binary.
- **Per-stage commit discipline:** coder commits per stage with the stage number in the commit subject (`coder Stage 0: foundation infra`). Auditor passes per stage. test-designer's failing-test contract lands as `tester v1: contract for Stage N` before coder begins each stage.
