# Plan — typed action returns (`typed-action-returns` + `test-report-typed-object`)

**From:** architect
**Branch:** `typed-action-returns` (off `runtime2`); sibling work on `test-report-typed-object` (also off `runtime2`)
**Inputs:** `.bot/typed-action-returns/builder/architect-handoff.md`, `.bot/test-report-typed-object/brief.md`
**Status:** v3.2 — warnings as channel (Ingi's reframe), not as Data field

## What v3.2 changed (from v3.1)

One reframe — but it's a good one:

- **Warnings are a channel, not a Data field.** Ingi proposed it; strictly better than my Data.Warnings shape change. The crystallized rule: **in-band for control flow, out-of-band for diagnostics.** `Error` stays on Data (fatal, caller short-circuits, must be in return path). Warnings go to a `warnings` channel alongside `Output` / `Input` / `Debug`. Action handlers write to the channel during Build() (and at runtime); the builder.validate pass subscribes and accumulates them into the build trace. No Data shape change. Same channel mechanism covers Build-time warnings AND runtime warnings (slow http, file.save overwrites, etc.). Multiple subscribers without aggregation logic.

## What v3.1 changed (from v3)

Two OBP critiques shaped that pass:

- **`BuildContext` and `BuildResult` killed.** Wrapper-classes-for-nothing. The action `@this` already carries args + step; Build() operates on `this`. Result is `Data` (universal result type — Ok with inferred type name, or Fail). A.6 contract collapsed to `Task<Data> Build()`.
- **`Data.As<T>()` framing dropped.** The caller shouldn't pick the materializer via a generic — that leaks the choice to the call site. Data owns the materializer lookup via its own `.Type`. A.4 reworded: property access goes through Data; `Data.As(string typeName)` exists only for explicit cross-type coercion.

## What v3 changed (from v2)

Five things from the second redline pass reshape the plan:

- **Per-action `Build()` method is now in scope** (was deferred in v2). Ingi's argument: you can't deliver strongly-typed-returns by LLM prompt teaching alone — the LLM is the *least* deterministic place to do mechanical inference. Build() moves the per-action type inference (file.read reads path extension, llm.query reads `schema`/`format` args, http.request reads URL extension) from Compile.llm prompt teaching into actual C# code on each handler. Wired into `builder.validate`. Section A.6 is now a real implementation section; A.4 collapses to just the lazy-materialization piece.
- **`tester/File` → `tester/Test` rename.** OBP-strict naming: `File` frames file-ness, but the actual identity is test-ness. `Test` pairs symmetrically with `Run` (the execution result) and `Results` (the collection). Doubled name `tester.Test` is the accepted OBP cost. `TestGoal` was the prefix-smell Ingi sniffed; `Goal` would collide with `app.goals.goal`. New A.7 section captures the rename.
- **The `(type)` hint lands on the step's *terminal* `variable.set`, not the next-op-after-the-action.** Step `- read file.csv, where zip=101, write to %list%` compiles to `read.file | list.where | variable.set` — through the chain, types may transform. The hint annotates the destination slot. Reinforces why Build() is the right place to compute the terminal Type — it sees the whole chain. A.5 + A.6 clarified.
- **Sequencing simplified.** Once `purge-systemio-from-actions` is merged into `runtime2`, it's invisible from here. Diagram and "after-purge-systemio" mentions dropped.
- **`goal.getTypes` strongly typed**, `mock.intercept → Mock`, `[PlangType("report")]` dropped, JUnit extension extends `GetByExtension` — all confirmed from v2.

## Decisions locked

| | |
|---|---|
| Cat C | dropped — builder already fixed it |
| `file.read` | `Data<object>` at C#; runtime already dispatches by extension via `ClrFromMime`; **Build() on file.read** annotates trailing `variable.set` Type from path-literal extension; `%var%(type)` hint overrides; falls back to `object` |
| `.json` files | parsed to `JsonNode` (already wired in `app/types/this.cs`) |
| `http.request` / `upload` | return `Data<Response>`; body dispatched by response `Content-Type` at runtime; **Build()** annotates trailing Type from URL extension; `(type)` hint overrides; unknown Content-Type → bytes (or text for `text/*`) |
| `llm.query` | `Data<object>` at C#; **Build()** annotates Type from `schema` arg (→ `json`) or `format` arg (`.md` → `md`, `csharp` → `csharp`, etc.) |
| `output.ask` | `Data<string>` at C#; user-provided `(type)` hint on the write target overrides; lazy materializer parses the string |
| `Response` record | `app/http/Response/this.cs` (no `Http` prefix — owner is in namespace) |
| `tester/File` → `tester/Test` | rename. `app.tester.Test.@this` at `app/tester/Test/this.cs`. `test.discover` returns `List<Test>`. See A.7. |
| `variable.set` | tags Data with `.Type=<T>`, **no parsing at set time**. Materialization is lazy on first access; **Data owns the materializer lookup via its own `.Type`** — caller never picks via a generic. |
| `(type)` hint | not new PLang syntax. Free-form text the LLM reads. Hint lands on the **step's terminal `variable.set`** Type, not on the immediate next op. |
| `Build()` method | new optional method on `IClass`: `Task<Data> Build()`. Runs from `builder.validate`. No wrapper classes — action `@this` carries its own args + step. Returns `Data` (Ok with inferred type name, or Fail). |
| Warnings | new `warnings` channel alongside `Output` / `Input` / `Debug`. Build() and runtime write non-fatal diagnostics there. Builder.validate subscribes; trace renders per-step. No Data shape change. **In-band Error stays on Data; out-of-band warnings go to the channel.** |
| `test.report` | pure compute → `Data<Report>`; persistence is explicit `file.save` in PLang; `Format` param dies; JUnit becomes `JunitSerializer` keyed on `.junit.xml` (multi-segment via extended `GetByExtension`) |
| `ReportResult` | not introduced; type is `Report`. No `[PlangType]` attribute — derived from class name. |

## What the homework confirmed

1. **`Run` already carries timings/output/error/userTags** (`app/tester/Run.cs`). No new `RunReport` projection — `Report.Runs` is the existing `Results` collection.
2. **Serializer registry exists** at `app/channels/serializers/this.cs` with `GetByContentType` / `GetByExtension` / `Register`. `JsonSerializer` is registered. `JunitSerializer` slots in.
3. **MIME→CLR mapping** lives at `PLang/app/types/this.cs ClrFromMime()` (CLAUDE.md still points at a stale `PLang/Runtime2/Engine/Utility/TypeMapping.cs` — proposal worth filing).
4. **`app/tester/File.cs` carries 9 test-domain fields beyond Path** (PrPath, EntryGoalName, Status, Directory, Goal, GoalHash, BuilderVersion, Tags, StatusReason) — confirms it's a real test entity, not a path wrapper. Per A.7, the type is renamed to `Test`.

## Branch A — `typed-action-returns` (off `runtime2`)

### A.1 Mechanical typings (12 handlers)

Change `Run()` from `Task<data.@this>` to `Task<data.@this<T>>`, fix any `SetProp` / value-construction. Verify each via the "Variables in scope" snapshot of a downstream step.

| Action | Handler | T |
|---|---|---|
| `test.discover` | `app/modules/test/discover.cs` | `List<Test>` (see A.7 — `File` renamed to `Test`) |
| `test.run` | `app/modules/test/run.cs` | `Results` |
| `goal.getTypes` | `app/modules/goal/getTypes.cs` | **strongly typed** — coder reads `Builder.Types(this)` and uses the actual record shape (likely a `TypeInfo` at `app/builder/Types/TypeInfo/this.cs`, or reuses the existing `app/types/` model if it fits). No more `Dictionary<string,string>`. |
| `output.ask` | `app/modules/output/ask.cs` | `string` |
| `channel.set` | `app/modules/channel/set.cs` | bare `Task<Data>` (void-like) |
| `mock.intercept` | (coder locates) | `Mock` — at `app/mock/Mock/this.cs` (doubled name fine per singular-folder rule) |
| `builder.types` | `app/modules/builder/types.cs` | new record at `app/builder/Types/this.cs` |
| `builder.actions` | `app/modules/builder/actions.cs` | new record at `app/builder/Actions/this.cs` |
| `builder.goals` | `app/modules/builder/goals.cs` | new record at `app/builder/Goals/this.cs` |
| `test.tag` | `app/modules/test/tag.cs` | low priority — `bool`; defer unless free |

For the three `builder.*` records: coder reads `Builder.Types/Actions/Goals(this)` and uses their actual return shapes. Records live in OBP-singular folders under `app/builder/`.

### A.2 Stay polymorphic at C# (2 handlers)

Only two are genuinely unknowable from the args alone:

| Action | Why |
|---|---|
| `goal.call` | returns whatever the called goal returns — needs cross-goal analysis Build() doesn't have |
| `settings.get` | polymorphic by key — depends on what got stored |

Three more — `file.read`, `http.request`/`upload`, `llm.query` — keep `Data<object>` at the C# layer but **become inferrable at compile time** via Build() (A.6).

### A.3 New `Response` record (HTTP)

`app/http/Response/this.cs`:

```csharp
public sealed record Response(
    int Status,
    Dictionary<string, string> Headers,
    object? Body,
    TimeSpan Duration);
```

`http.request` and `http.upload` both return `Task<data.@this<Response>>`. The `Body` field is populated at runtime by:

1. Parse `Content-Type` from response headers.
2. Look up serializer via `Serializers.GetByContentType(ct)`.
3. Deserialize body using the serializer.
4. Unknown / no `Content-Type` → keep as `byte[]`; `text/*` with unknown subtype → `string`.

`http.download` is unchanged (its job is to save a file, not return parsed content).

### A.4 `variable.set` — declared type, lazy materialization

Runtime mechanism. `variable.set Type=<T>` tags the Data object with `.Type=<T>` and stores the raw value untouched. **No parsing at set time** — same discipline as Lazy params and `IBooleanResolvable`'s deferred I/O.

**Data owns the materializer lookup, not the caller.** A consumer never picks the materializer via a generic — `Data.As<JsonNode>()` is the wrong shape because it leaks the materializer choice to the call site. Instead:

- **Default case.** Property dereference (`%var.rows%`, `%var.foo.bar%`) and any C# accessor that wants the materialized value goes through a Data method that uses Data's own declared `.Type` to look up the materializer from the `Serializers` registry, runs it once on `Data.Value`, caches the result. Exact name (`Materialize()`, internal `Value` getter override, etc.) is the coder's call — the rule is that Data picks the materializer from `.Type`, not the caller.
- **Cross-type coercion.** `Data.As(string typeName)` for the rare case where someone has csv-tagged data and explicitly wants it as a different type. Data looks up the materializer for the *requested* type and runs it. Still owned by Data; the parameter is the requested target, not a generic.

Discipline: nothing in `variable.set` does I/O or parsing. It records intent. If the declared `.Type` has no materializer registered, the error surfaces at the first access site, not at set time — that's where the developer's mental model lives (the access is where they expected the typed shape).

This section is purely runtime. Compile-time type inference is A.5 + A.6 below.

### A.5 The `(type)` hint — prompt convention, lands on terminal `variable.set`

Devs write `file.read %path%(csv)` or `write to %answer%(json)` in step text. The LLM reads it; **PLang's tokenizer doesn't parse it; C# parses nothing.** It's free-form prose like any other text in step descriptions.

The hint lands on the **step's terminal `variable.set`** — not on the op immediately after the action. A step like:

```plang
- read file.csv, where zip=101, write to %list%
```

compiles to a chain `read.file | list.where | variable.set`. Through the chain, types may transform — `read.file` produces csv-tagged data, `list.where` filters it, the terminal `variable.set` carries whatever Type is appropriate for `%list%`. The `(type)` hint, if present on the write target, annotates that terminal slot.

One Compile.llm rule:

> When a variable reference or string literal is followed by `(type)` in a write-target position, treat `type` as a PLang type hint for the destination slot. Use it as the Type on the step's terminal `variable.set`. Unknown type names should still be passed through — coercion happens at runtime, where the lazy materializer (A.4) will surface a clear error on first access.

The rule lives in `Compile.llm` (cross-cutting kernel teaching). No schema change. No parser work.

### A.6 Per-action `Build()` method — in scope

This is the v3 keystone. Per-action C# code does the deterministic compile-time inference that the LLM is the wrong tool for.

**Contract.** No wrapper classes — the action `@this` already carries its args (source-generated properties) and reaches its Step. Result is `Data` (universal result type, no separate `BuildResult` needed):

```csharp
public interface IClass
{
    // existing surface...
    Task<Data> Build();   // optional; default impl returns Data.Ok()
}
```

`Build()` is optional — handlers that don't need compile-time inference just don't implement it (or return `Data.Ok()`). The validator iterates each step's actions, calls `Build()` on each, accumulates the results, and sets the terminal `variable.set` Type accordingly.

**Return convention via Data.**

- `Data.Ok("csv")` — success; `Data.Value` carries the inferred terminal type name. Validator reads it and sets the terminal `variable.set` Type.
- `Data.Fail(error)` — build-time error (e.g., contradictory args). Validator surfaces it; emit fails.
- `Data.Ok()` (no value) — Build() ran but had nothing to contribute. Normal.

Non-fatal diagnostics (warnings) don't go through the return value — they go on the warnings channel, see below.

**Hook point.** `builder.validate` — by the time validate runs the actions are resolved, args are bound, and emit hasn't happened yet, so Build() can influence the .pr output. Coder verifies the existing validate pass shape and slots Build() in; if validate doesn't currently iterate per-action, that's the one piece of plumbing this branch lands.

**Concrete per-action implementations.**

```csharp
// file.read
public async Task<Data> Build()
{
    if (Path.Value is string p)
    {
        if (!System.IO.File.Exists(p))
            await Channels.Warnings.WriteAsync(new MissingFileAtBuild(p));

        if (System.IO.Path.HasExtension(p))
        {
            var mime = Context.App.Formats.Mime(System.IO.Path.GetExtension(p));
            return Data.Ok(data.type.FromMime(mime).Name);
        }
    }
    return Data.Ok();
}

// llm.query
public Task<Data> Build()
    => !string.IsNullOrEmpty(Schema.Value) ? Task.FromResult(Data.Ok("json"))
     : !string.IsNullOrEmpty(Format.Value) ? Task.FromResult(Data.Ok(Format.Value))
     : Task.FromResult(Data.Ok());

// http.request / http.upload — same pattern against URL literal
```

`output.ask.Build()` returns `Data.Ok()` — defaults handled by the `(type)` hint in A.5.

**Warnings via channel (new).** Build() — and runtime, by symmetry — emits non-fatal diagnostics by writing to a `warnings` channel, alongside `Output` / `Input` / `Debug`:

```csharp
// In any action's Build() or Run() that wants to warn:
await Channels.Warnings.WriteAsync(new MissingFileAtBuild(p));
```

The framing: **in-band for control flow, out-of-band for diagnostics.** Errors stop computation; they must travel in the return path on Data. Warnings don't stop computation; they're a side stream. The channels system already exists for redirectable I/O — `warnings` slots in naturally.

Subscribers:

- **Builder.validate** subscribes during the build pass, accumulates warnings into the build trace alongside the inferred types from Build()'s return.
- **CLI / debug output** can subscribe to surface warnings to stderr or the debug panel.
- **`--strict` build mode** (future) subscribes and fails the build on any warning.
- At runtime: same channel, same writes. Slow http, file.save overwrite, llm.query rate-limit hit — anything that wants to warn writes here.

**`IWarning` shape:** start small — `Message` + caller identity (which the channel infra tags automatically, see open note). Grows with use. No global `IDiagnostic` unification this branch.

**Source attribution** — when a warning lands, the consumer needs to know which action wrote it. Lean: channel-side scoping, same pattern as `Debug.Write`. The channel infra tags writes with caller identity automatically; the action writer just says "warn about this." Coder verifies what the existing channels mechanism gives us; if `Debug.Write` already tags with caller info, `Warnings` inherits the same.

**User cast wins over Build() inference.** If the LLM already emitted a `(type)` hint into the terminal `variable.set` Type via A.5, Build() doesn't override it. Build() fills in the deterministic inference where the user didn't specify.

**Why not just teach the LLM these rules?** Two reasons. (1) Determinism — extension lookup is a 1:1 table, not something an LLM should be probabilistically deciding. (2) Testability — `file.read.Build()` gets a unit test; "tell the LLM about extensions" gets a hope and a prayer.

### A.7 `tester/File` → `tester/Test` rename

Today: `app/tester/File.cs` named `File`, with `[PlangType("testfile")]`. Carries Path + 9 test-domain fields (PrPath, EntryGoalName, Status, Directory, Goal, GoalHash, BuilderVersion, Tags, StatusReason).

After: `app/tester/Test/this.cs` named `Test`. Class name derives the PLang type (`test`) — no attribute needed.

**Why `Test` and not the alternatives:**

- **`TestGoal`** — the prefix-smell Ingi flagged. `{Domain}{Thing}` naming is what OBP-strict design avoids.
- **`Goal`** — collides nominally with `app.goals.goal.@this`. Aliases get confusing.
- **`File`** — frames file-ness over test-ness. The fact that it lives in a `*.test.goal` file is incidental; the identity is "a test that's been discovered and is ready to run."
- **`Test`** — noun of the tester domain. Pairs symmetrically: a `Test` is discovered → gets `Run` → produces a `Run` (the result) → collected in `Results`. Reads naturally; OBP-clean by doubled name.

**Touch surface.**

- Rename `app/tester/File.cs` → `app/tester/Test/this.cs`; class `File` → `Test`; drop `[PlangType("testfile")]`.
- `test.discover` (A.1): return `List<Test>`.
- `test.run` (A.1): input is `Test` (was `File`).
- `test.report` (B.2): `Report.Runs` carries `Run`s; nothing changes there (Run already references File via property — that becomes Test).
- C# unit tests that reference `tester.File`: update.
- Any consumer reading `%file.Path%` style properties — Path stays a property on Test, so call sites stay valid; only the type name changes.

Scope cost is real but contained. The rename is bigger than a one-line signature change, but smaller than locking in the wrong name and having to migrate later.

## Branch B — `test-report-typed-object` (off `runtime2`)

Forks from `runtime2`, independent of `typed-action-returns`.

### B.1 New `Report` type

`app/tester/Report/this.cs`:

```csharp
public sealed record Report(
    Dictionary<Status, int> Summary,
    string BuilderVersion,
    Results Runs,
    Coverage BranchCoverage);
```

No `[PlangType("report")]` — the PLang type name derives from the class name (lowercased). The attribute is a drift hazard. Legitimate uses exist when the class name would collide with a module name (the old `tester/File` used it to avoid colliding with the `file` module — now moot post-A.7), but `Report` has no collision.

`Runs` is the existing `Results` collection — no new projection. `Run` already carries `CapturedOutput`, `Duration`, `Error`, `UserTags`.

### B.2 Refactored `test.report`

```csharp
public sealed partial record report : IClass { ... }

public partial class ReportHandler
{
    public async Task<data.@this<Report>> Run()
    {
        var report = new Report(
            Summary: Results.Value.Summary(),
            BuilderVersion: BuilderVersion(),
            Runs: Results.Value,
            BranchCoverage: ComputeCoverage());
        return data.@this<Report>.Ok(report);
    }
}
```

Gone: `BuildJson`, `BuildJUnit`, all `System.IO`, the `Format` param, `OutputDirectory` param. Handler shrinks to ~15 lines.

### B.3 `JunitSerializer` (new)

`app/channels/serializers/serializer/Junit.cs`:

```csharp
public sealed class Junit : ISerializer
{
    public string ContentType => "application/junit+xml";
    public string FileExtension => ".junit.xml";
    // Serialize knows how to render Report -> JUnit XML.
    // The existing BuildJUnit body moves here verbatim.
}
```

Registered in `serializers/this.cs` constructor. `file.save` dispatches by extension. The registry's `GetByExtension` is extended to walk multi-segment extensions (e.g. `.junit.xml`).

### B.4 Calling-site change

`os/system/test.goal`:

```plang
- test.report results %results%, write to %report%
- file.save %report% to '.test/results.json'
- file.save %report% to '.test/junit.xml'        // when junit wanted
```

The second `file.save` is conditional; goals that don't want JUnit omit it. No `Format` param to switch on anywhere.

### B.5 Tests

- **C# unit:** construct fake `Results` (passes/fails/skips/timeouts), call `test.report`, assert `Data<Report>` shape, assert no `System.IO` calls happen.
- **C# snapshot:** JSON-serialize the `Report` via the registry and pin output. Cycle-safety falls out of pruning at capture (existing `e6b0e9ea1` fix).
- **PLang `.test.goal`** under `Tests/TestModule/Report/`: assert `%report.Summary.Pass%`, `%report.Runs.0.Test.Path%` (now via Test, per A.7), `%report.Runs.0.Error.Message%`. Existing tests against `%report.reportPath%` / `%report.content%` move to `file.save`-based assertions on the file.

## Verification recipe

After each typing change in A.1:

```bash
cd /workspace/plang/Tests
rm -rf <relevant>/.build
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build '--build={"cache":false}'
```

Inspect the trace's "Variables in scope" snapshot for a downstream step — should now show `%var%(<T>)` instead of `%var%(object)`. The trailing `variable.set` in the .pr should carry `type:"<T>"`.

For A.6 (Build()) verification: build a test goal that does `file.read 'foo.csv', write to %x%` and confirm `%x%(csv)` shows up downstream. Same for `llm.query schema=...` (expect `(json)`) and `http.request 'http://api/x.json'` (expect `(json)`).

For A.5 (cast hint) verification: build a step with `output.ask "give me stuff", write to %answer%(json)` and confirm the terminal `variable.set` carries `Type=json` and the materializer parses the user response on first property access.

Across the `Tests/` corpus, the 729 `(object)` occurrences in Category A should disappear or move into Category B (`goal.call`, `settings.get` — where the genuine polymorphism stays honest).

## Open notes for plan refinement

1. **JUnit extension dispatch.** `GetByExtension` walks multi-segment extensions (`.junit.xml`). One-line registry extension; coder lands it as part of B.3.
2. **`builder.types` / `actions` / `goals` records** — coder reads current implementations and names them; OBP-singular folders under `app/builder/`.
3. **`mock.intercept`** location — flagged in original survey; coder locates and types as `Mock`.
4. **`test.tag`** is marginal — coder's discretion. Worth it if one-liner.
5. **`variable.set` materializer table** — `variable.set` itself only tags Data with `.Type`; the lazy materializers (`json` → `JsonNode`, `csv` → `Csv`, `xlsx` → `Workbook`, …) live on `Data.As<T>()` / property access, hooked through the existing `Serializers` registry where possible. `Primitives` table at `app/types/this.cs` is the source of truth for which types exist.
6. **`[PlangType]` derivation** — follow-up: source generator could derive PLang type from class name (lowercased) by default, with `[PlangType("…")]` as explicit override only on collision. CLAUDE.md proposal worth filing.
7. **CLAUDE.md proposal** — file under `.bot/typed-action-returns/claude-md-proposals.md`: the "Key Files" section still references `PLang/Runtime2/Engine/Utility/TypeMapping.cs`; the actual location is `PLang/app/types/this.cs ClrFromMime`.
8. **`Build()` contract refinement** — first cut in A.6 is intentionally minimal (returns Data carrying type-name string, or Ok/Fail, plus optional Warnings). Other compile-time signals can grow into Data.Properties without re-litigating the Build() shape.
9. **Warnings channel API shape** — TBD by coder. Lean on whatever pattern `Debug.Write` already uses for caller-identity tagging. The new `warnings` channel slots into the existing `Output` / `Input` / `Debug` infrastructure. `IWarning` starts as just `Message` + auto-tagged caller; grows with use.
10. **Data API shape for materialization** — A.4 leaves the exact method name open (`Materialize()` vs internal `Value` getter override vs `As(string)` for cross-type). Coder picks during implementation; the locked rule is that Data picks the materializer from `.Type`, never the caller via a generic.
