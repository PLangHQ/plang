# Plan — typed action returns (`typed-action-returns` + `test-report-typed-object`)

**From:** architect
**Branch:** `typed-action-returns` (off `runtime2`); sibling work on `test-report-typed-object` (also off `runtime2`)
**Inputs:** `.bot/typed-action-returns/builder/architect-handoff.md`, `.bot/test-report-typed-object/brief.md`
**Status:** v2 — addresses Ingi redline at `architect/comments.json`

## What v2 changed (redline-driven)

Two of Ingi's comments collapse big sections of v1:

- **`%var%(type)` is not new PLang syntax.** It's text the LLM reads. C# parses nothing. No castType field in the .pr. The cast-hint is purely a **Compile.llm prompt convention**. Section A.5 dropped from ~30 lines to one paragraph; the "Persisting castType" open note dies entirely (there's nothing separate to persist — the cast just decorates the existing `Type` field on the trailing `variable.set`).
- **`variable.set` is the universal converter.** Every action's capture already lands as `variable.set Value=%!data% Type=<T>`. If `variable.set` knows how to coerce raw data into the named type (Type=`json` → parse string to `JsonNode`; Type=`csv` → parse to `Csv`; …), then **any** action becomes type-coercible without the action itself knowing about types. `output.ask`, `file.read`, `http.request`, `llm.query` all stop being special cases — they each just need their trailing `variable.set` annotated with the right Type, and the converter handles the rest.

These two together delete most of v1's section A.4/A.5 infrastructure and turn type inference into a prompt-language story plus a small converter table on `variable.set`.

Other redline points absorbed below: `MockHandle` → `Mock`; `goal.getTypes` strongly typed; `llm.query` moves out of polymorphic (it's inferrable from `schema`/`format` args); `[PlangType("report")]` dropped (derive from class name); sequencing — all three branches fork from `runtime2`, Ingi merges `purge-systemio-from-actions` into `typed-action-returns` when ready; per-action `Build()` method deferred to its own branch.

## Decisions locked

| | |
|---|---|
| Cat C | dropped — builder already fixed it |
| `file.read` | `Data<object>` at C#; runtime already dispatches by extension via `ClrFromMime`; **trailing `variable.set` Type annotated** by LLM from path-literal, from `%var%(type)` hint, or left `object` |
| `.json` files | parsed to `JsonNode` (already wired in `app/types/this.cs`) |
| `http.request` / `upload` | return `Data<Response>`; body dispatched by response `Content-Type` at runtime; trailing `variable.set` Type annotated from URL extension, `(type)` hint, or left `object`; unknown Content-Type → bytes (or text for `text/*`) |
| `llm.query` | `Data<object>` at C#; trailing `variable.set` Type annotated from `schema` arg (→ `json`) or `format` arg (`.md` → `md`, `csharp` → `csharp`, etc.) |
| `output.ask` | `Data<string>` at C#; trailing `variable.set` Type can be overridden by `write to %answer%(json)` style hint; converter on `variable.set` parses the string |
| `Response` record | lives at `app/http/Response/this.cs` (drop the `Http` prefix — owner is in namespace) |
| `test.report` | pure compute → `Data<Report>`; persistence is explicit `file.save` in PLang; `Format` param dies; JUnit becomes `JunitSerializer` keyed on `.junit.xml` |
| `ReportResult` | not introduced; type is `Report` |
| Sequencing | all three branches fork from `runtime2`; Ingi merges `purge-systemio-from-actions` into `typed-action-returns` when done; `test-report-typed-object` is independent of both until its own time |

## What the homework confirmed

1. **`Run` already carries timings/output/error/userTags** (`app/tester/Run.cs`). No new `RunReport` projection — `Report.Runs` is the existing `Results` collection (`IEnumerable<Run>` with `Summary()`).
2. **Serializer registry already exists** at `app/channels/serializers/this.cs` with `GetByContentType` / `GetByExtension` / `Register`. `JsonSerializer` is registered. `JunitSerializer` slots in.
3. **MIME→CLR mapping** lives at `PLang/app/types/this.cs ClrFromMime()` (CLAUDE.md still points at a stale `PLang/Runtime2/Engine/Utility/TypeMapping.cs` — proposal worth filing).
4. **`app/tester/File.cs` is a real test-domain type**, not a path wrapper — carries 9 fields beyond Path (`PrPath`, `EntryGoalName`, `Status`, `Directory`, `Goal`, `GoalHash`, `BuilderVersion`, `Tags`, `StatusReason`). So `test.discover` returns `List<File>`, not `List<path>`.

## Branch A — `typed-action-returns` (off `runtime2`)

### A.1 Mechanical typings (12 handlers)

Change `Run()` from `Task<data.@this>` to `Task<data.@this<T>>`, fix any `SetProp` / value-construction. Verify each via the "Variables in scope" snapshot of a downstream step.

| Action | Handler | T |
|---|---|---|
| `test.discover` | `app/modules/test/discover.cs` | `List<File>` (File carries 9 fields — real test-domain object) |
| `test.run` | `app/modules/test/run.cs` | `Results` |
| `goal.getTypes` | `app/modules/goal/getTypes.cs` | **strongly typed** — coder reads `Builder.Types(this)` and extracts the actual shape into a record at `app/builder/Types/TypeInfo/this.cs` (or reuses an existing type model if one fits). No more `Dictionary<string,string>`. |
| `output.ask` | `app/modules/output/ask.cs` | `string` |
| `channel.set` | `app/modules/channel/set.cs` | bare `Task<Data>` (void-like) |
| `mock.intercept` | (coder locates) | `Mock` — at `app/mock/Mock/this.cs` (doubled name is fine per singular-folder rule) |
| `builder.types` | `app/modules/builder/types.cs` | new record at `app/builder/Types/this.cs` |
| `builder.actions` | `app/modules/builder/actions.cs` | new record at `app/builder/Actions/this.cs` |
| `builder.goals` | `app/modules/builder/goals.cs` | new record at `app/builder/Goals/this.cs` |
| `test.tag` | `app/modules/test/tag.cs` | low priority — `bool`; defer unless free |

For the three `builder.*` records: coder reads the current `Builder.Types/Actions/Goals(this)` implementations and uses their actual return shapes. Records live in OBP-singular folders under `app/builder/`. Naming follows the Law of Names (`Builder{Capability}` or the inner type name; coder decides based on the actual return).

### A.2 Stay polymorphic at C# (2 handlers)

Only two are genuinely unknowable from the args:

| Action | Why |
|---|---|
| `goal.call` | returns whatever the called goal returns — needs cross-goal analysis the planner doesn't have |
| `settings.get` | polymorphic by key — depends on what got stored |

Three more — `file.read`, `http.request`/`upload`, `llm.query` — keep `Data<object>` at the C# layer but **become inferrable at compile time** via the prompt rules in A.4.

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

### A.4 Trailing `variable.set` — universal converter

This is the keystone. Every action's capture is `variable.set Value=%!data% Type=<T>`. Two changes make all the inferrable-but-polymorphic actions land their types in the snapshot:

**(a) `variable.set` becomes a converter.** When the runtime executes `variable.set` with a `Type` it doesn't already match (data carries one type, the slot's Type wants another), it coerces via the serializer registry. Type=`json` + raw string → `JsonNode`. Type=`csv` + raw string → `Csv`. Type=`xlsx` + bytes → `Workbook`. The same `Serializers` registry that A.3 uses on HTTP responses handles this. If no converter exists for a given Type, `variable.set` errors clearly.

**(b) The LLM annotates the trailing `variable.set`'s Type from action-specific cues.** Compile.llm gets a small block of per-action rules:

- **`file.read`** — if path arg is a literal, look at the extension, emit Type from `ClrFromMime`. If `%var%(type)` hint on the path, use that. Otherwise leave as `object`.
- **`http.request` / `http.upload`** — same against the URL: literal URL extension drives the type; `(type)` hint wins.
- **`llm.query`** — if `schema` arg is set, emit Type=`json`. If `format` arg is set (`.md`, `csharp`, etc.), emit that as Type.
- **`output.ask`** — defaults to `string`; if the write target carries a hint (`write to %answer%(json)`), use that — converter on `variable.set` parses the user's response.

These are prompt rules, not parser rules. The LLM already reads action args; it just gets explicit guidance on how to fill `Type` for these handlers. No C# changes for the inference itself — only the per-action notes in `MarkdownTeaching` (the `*.notes.md` files for each action, per the existing per-action LLM notes convention).

### A.5 The `(type)` hint — prompt convention, not syntax

Devs write `file.read %path%(csv)` or `write to %answer%(json)` in goal text. The LLM reads it and uses the type when annotating the trailing `variable.set` Type. **PLang's tokenizer doesn't parse it. C# parses nothing.** It's free-form text the LLM interprets — same surface that lets devs write any prose in step text.

One Compile.llm rule:

> When a variable reference or string literal is followed by `(type)` in an action argument, treat `type` as a PLang type hint for the slot. If the action would emit a trailing `variable.set` for that arg's data, use the hint as the Type. Unknown type names should still be passed through — coercion happens at runtime, where `variable.set` will error if it can't convert.

The rule lives in `Compile.llm` (or as cross-cutting kernel teaching). Per-action notes in A.4 reference the convention but don't re-define it. No schema change. No parser work.

### A.6 Per-action `Build()` method (deferred)

Ingi's idea (comment 6b2c267f8f): action handlers could expose a `Build()` method that runs at builder time and propagates compile-time information (like inferred result type from a literal arg) to the builder. This generalizes A.4's prompt rules into actual code — the planner would call `read.Build()` and read back "the type for the trailing variable.set is `csv`."

**Deferred to its own branch.** A.4 + A.5 ship the LLM-only inference; the `Build()` method generalization comes later. Filed as `Documentation/Runtime2/todos.md` candidate.

## Branch B — `test-report-typed-object` (off `runtime2`)

Forks from `runtime2`, independent of `typed-action-returns`. `purge-systemio-from-actions` is merged into this branch (or vice versa, Ingi's call) when it lands so the `report.cs` rewrite picks up the System.IO purge.

### B.1 New `Report` type

`app/tester/Report/this.cs`:

```csharp
public sealed record Report(
    Dictionary<Status, int> Summary,
    string BuilderVersion,
    Results Runs,
    Coverage BranchCoverage);
```

No `[PlangType("report")]` — the PLang type name derives from the class name (lowercased). The attribute is a drift hazard. Existing uses like `[PlangType("testfile")]` on `app/tester/File.cs` are legitimate (the class name `File` would collide with the `file` module), but `Report` has no collision so no attribute.

`Runs` is the existing `Results` collection — no new projection. `Run` already carries `CapturedOutput`, `Duration`, `Error`, `UserTags`. JSON snapshots can rely on a stable shape because `Run`'s properties are stable.

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

Gone: `BuildJson`, `BuildJUnit`, all `System.IO`, the `Format` param, `OutputDirectory` param. The handler shrinks to ~15 lines.

### B.3 `JunitSerializer` (new)

`app/channels/serializers/serializer/Junit.cs`:

```csharp
public sealed class Junit : ISerializer
{
    public string ContentType => "application/junit+xml";
    public string FileExtension => ".junit.xml";
    // Serialize knows how to render Report -> JUnit XML.
    // The existing BuildJUnit body moves here verbatim (Report -> XML projection).
}
```

Registered in `serializers/this.cs` constructor alongside `Json`, `Text`, etc. `file.save` dispatches by extension. The registry's `GetByExtension` is extended to walk multi-segment extensions (per Ingi: "yes, extend").

### B.4 Calling-site change

`os/system/test.goal`:

```plang
- test.report results %results%, write to %report%
- file.save %report% to '.test/results.json'
- file.save %report% to '.test/junit.xml'        // when junit wanted
```

The second `file.save` is conditional in goals that want JUnit output; goals that don't omit it. No `Format` param to switch on anywhere.

### B.5 Tests

- **C# unit:** construct fake `Results` (passes/fails/skips/timeouts), call `test.report`, assert `Data<Report>` shape, assert no `System.IO` calls happen.
- **C# snapshot:** JSON-serialize the `Report` via the registry and pin output. Cycle-safety falls out of pruning at capture (per the existing `e6b0e9ea1` fix) — snapshot enforces no cycles.
- **PLang `.test.goal`** under `Tests/TestModule/Report/`: assert `%report.Summary.Pass%`, `%report.Runs.0.File.Path%`, `%report.Runs.0.Error.Message%`. Existing tests against `%report.reportPath%` / `%report.content%` move to `file.save`-based assertions on the file rather than on the action return.

## Sequencing & coordination

```
                          runtime2
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
purge-systemio-from-          typed-action-         test-report-typed-
actions  (in flight)          returns               object
        │                     │                     │
        └──── merged into ────┘                     │
                              │                     │
                              └──── lands ────────┐ │
                                                  ▼ ▼
                                              future runtime2 merges
```

All three forks of `runtime2`. Ingi merges `purge-systemio-from-actions` into `typed-action-returns` when ready. `test-report-typed-object` is independent of both during its own work; when it lands it picks up whatever's in `runtime2` at the time.

## Verification recipe

After each typing change in A.1:

```bash
cd /workspace/plang/Tests
rm -rf <relevant>/.build
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build '--build={"cache":false}'
```

Inspect the trace's "Variables in scope" snapshot for a downstream step that uses the variable — should now show `%var%(<T>)` instead of `%var%(object)`. The `.pr`'s trailing `variable.set` should carry `type:"<T>"`.

For A.4 changes (per-action inference): also build a test goal that does `file.read 'foo.csv', write to %x%` and confirm `%x%(csv)` shows up downstream. Same for `llm.query schema=...` (expect `(json)`) and `http.request 'http://api/x.json'` (expect `(json)`).

Across the `Tests/` corpus, the 729 `(object)` occurrences in Category A should disappear or move into Category B (where the genuine polymorphism stays honest — `goal.call`, `settings.get`).

## Open notes for plan refinement

1. **JUnit extension dispatch.** `GetByExtension` walks multi-segment extensions (`.junit.xml`). One-line registry extension; coder lands it as part of B.3.
2. **`builder.types` / `actions` / `goals` records** — coder reads current implementations and names them; OBP-singular folders under `app/builder/`.
3. **`mock.intercept`** location — flagged in original survey; coder locates and types as `Mock`.
4. **`test.tag`** is marginal — coder's discretion. Worth it if one-liner.
5. **`variable.set` converter table** — coder defines the initial coercions (`json` ↔ string, `csv` ↔ string, `xlsx` ↔ bytes, …). Hooked through the existing `Serializers` registry where possible; `Primitives` table at `app/types/this.cs` is the source of truth for which types exist.
6. **`[PlangType]` derivation** — follow-up: source generator could derive PLang type from class name (lowercased) by default, with `[PlangType("…")]` as explicit override only on collision. Filed as a CLAUDE.md / good-to-know proposal in this branch's `claude-md-proposals.md`.
7. **CLAUDE.md proposal** — file under `.bot/typed-action-returns/claude-md-proposals.md`: the "Key Files" section still references `PLang/Runtime2/Engine/Utility/TypeMapping.cs`; the actual location is `PLang/app/types/this.cs ClrFromMime`.
8. **Per-action `Build()` method (deferred)** — Ingi's idea for action handlers to expose a builder-time hook. Generalizes A.4's prompt rules into code. Own branch later; capture in `todos.md`.
