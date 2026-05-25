# Plan — typed action returns (`typed-action-returns` + `test-report-typed-object`)

**From:** architect
**Branch:** `typed-action-returns` (off `runtime2`); sibling work on `test-report-typed-object`
**Inputs:** `.bot/typed-action-returns/builder/architect-handoff.md`, `.bot/test-report-typed-object/brief.md`
**Status:** draft for Ingi redline

## Decisions locked

| | |
|---|---|
| Cat C | dropped — builder already fixed it |
| `file.read` | `Data<object>` at C#; runtime already dispatches by extension via `ClrFromMime`; **builder** annotates result type from path-literal, `%var%(type)` dev hint, or falls back to `(object)` |
| `.json` files | parsed to `JsonNode` (already wired in `app/types/this.cs`) |
| `http.request` / `upload` | return `Data<Response>`; body dispatched by response `Content-Type`; URL extension is fallback hint; dev override via `(type)`; unknown → bytes (or text for `text/*`) |
| `Response` record | lives at `app/http/Response/this.cs` (drop `Http` prefix — owner is in namespace) |
| `test.report` | pure compute → `Data<Report>`; persistence is explicit `file.save` in PLang; `Format` param dies; JUnit becomes `JunitSerializer` keyed on `.junit.xml` |
| `ReportResult` | not introduced; type is `Report` |
| Sequencing | `purge-systemio-from-actions` → `typed-action-returns` → `test-report-typed-object` |

## What the homework changed

Three things from the existing-code survey shape the work:

1. **`Run` already carries timings/output/error/userTags** (`app/tester/Run.cs`). No new `RunReport` projection needed — `Report.Runs` can be the existing `Results` collection (`IEnumerable<Run>` with `Summary()`).
2. **Serializer registry already exists** at `app/channels/serializers/this.cs` with `GetByContentType` / `GetByExtension` / `Register`. `JsonSerializer` is registered. `JunitSerializer` just slots in.
3. **`%var%(type)` syntax does not exist today.** The variable resolver at `app/variables/this.cs:603` parses only `%name%`. The cast-hint is a deliberate **language addition** — own section below.

Also worth flagging: CLAUDE.md still references `PLang/Runtime2/Engine/Utility/TypeMapping.cs`. That path no longer exists; the MIME→CLR mapping is at `PLang/app/types/this.cs ClrFromMime()`. CLAUDE.md proposal worth filing separately.

## Branch A — `typed-action-returns` (off `runtime2`)

### A.1 Mechanical typings (12 handlers, no design decisions)

Change `Run()` from `Task<data.@this>` to `Task<data.@this<T>>`, fix any `SetProp` / value-construction. Verify each via the "Variables in scope" snapshot of a downstream step.

| Action | Handler | T |
|---|---|---|
| `test.discover` | `app/modules/test/discover.cs` | `List<File>` |
| `test.run` | `app/modules/test/run.cs` | `Results` |
| `goal.getTypes` | `app/modules/goal/getTypes.cs` | `List<Dictionary<string,string>>` (concrete already; just annotate) |
| `output.ask` | `app/modules/output/ask.cs` | `string` |
| `channel.set` | `app/modules/channel/set.cs` | bare `Task<Data>` (void-like, no return advertised) |
| `mock.intercept` | (coder locates) | `MockHandle` |
| `builder.types` | `app/modules/builder/types.cs` | new `BuilderTypes` record at `app/builder/Types/this.cs` |
| `builder.actions` | `app/modules/builder/actions.cs` | new `BuilderActions` record at `app/builder/Actions/this.cs` |
| `builder.goals` | `app/modules/builder/goals.cs` | new `BuilderGoals` record at `app/builder/Goals/this.cs` |
| `test.tag` | `app/modules/test/tag.cs` | low priority — `bool`; defer unless free |

For the three `builder.*` records: coder reads the current `Builder.Types(this)` / `Builder.Actions(this)` / `Builder.Goals(this)` and uses their actual return shapes. Records live in OBP-singular folders under `app/builder/`. Naming is `Builder{Capability}` per the Law of Names (`/memory/naming_convention_design.md`).

### A.2 Stay polymorphic (4 handlers, no signature change)

| Action | Why |
|---|---|
| `llm.query` | genuinely polymorphic; schema-aware typing comes later as its own design |
| `goal.call` | returns whatever the called goal returns |
| `settings.get` | polymorphic by key |
| `file.read` | polymorphic by extension; the **value** carries the typed thing, signature stays `Data<object>` |

### A.3 New `Response` record (HTTP)

`app/http/Response/this.cs`:

```csharp
public sealed record Response(
    int Status,
    Dictionary<string, string> Headers,
    object? Body,
    TimeSpan Duration);
```

`http.request` and `http.upload` both return `Task<data.@this<Response>>`. The `Body` field is populated by:

1. Parse `Content-Type` from response headers.
2. Look up serializer via `Serializers.GetByContentType(ct)`.
3. Deserialize body using the serializer.
4. Unknown / no `Content-Type` → keep as `byte[]`; `text/*` with unknown subtype → `string`.

`http.download` is unchanged (its job is to save a file, not return parsed content).

### A.4 Builder-time type annotation

This is the piece that makes A.2's `file.read` and the HTTP actions actually useful in the snapshot.

For `file.read`: when the planner emits the trailing `variable.set Value=%!data%`, it inspects the input args:

- **Literal path?** `Path.GetExtension(arg)` → `Formats.Mime(ext)` → `data.type.FromMime(mime)` → emit `type:"<plang-name>"`.
- **`%var%(type)` cast hint?** Use that type directly (the cast wins over inference).
- **Pure `%var%` with no hint?** Emit `type:"object"` (current honest fallback).

For `http.request` and `http.upload`: same mechanism against the URL — extension on the URL path drives the inferred `Body` type. Cast hint on the URL (`http.request 'http://api/x'(json)`) wins.

Both annotations come from the **same TypeMapping the runtime already uses** — `app/types/this.cs ClrFromMime`. No new infra; the planner just calls it during catalog emission.

### A.5 The `%var%(type)` cast syntax

New language-level annotation. The builder parses `(type)` after a variable reference (or string literal) in an arg slot and uses it as a static type hint.

**Where it appears.**
- Args to actions: `file.read %path%(csv)`, `http.request %url%(json)`.
- Eventually anywhere the dev wants to override inference — same syntax everywhere.

**Where it does NOT appear.**
- Builder-time hint only. Runtime never sees it; the resolver at `app/variables/this.cs:603` does not change.
- Does not affect runtime dispatch — `file.read` still reads what's on disk.

**Builder change.** The compile prompt is extended with one rule: when a variable (or literal) is followed by `(type)` in an arg, treat that type as the variable's PLang type for the purpose of annotating downstream `variable.set` ops. The compile-output schema gains an optional `castType` field on arg references; the planner emits it; the trailing `variable.set` reads it.

**Validation.** Unknown type → builder warning (`"unknown type 'xls', did you mean 'xlsx'?"`). The prompt rules and the validator pull from the same `Primitives` dict at `app/types/this.cs`.

**Open question.** Does the cast survive into the `.pr` so runtime can use it for things like format coercion later? My read: **pure compile-time for now**, can persist later if a use case emerges. Don't widen the contract before there is a consumer.

## Branch B — `test-report-typed-object` (lands after A + purge-systemio)

### B.1 New `Report` type

`app/tester/Report/this.cs`:

```csharp
[PlangType("report")]
public sealed record Report(
    Dictionary<Status, int> Summary,
    string BuilderVersion,
    Results Runs,
    Coverage BranchCoverage);
```

`Runs` is the existing `Results` collection — no new projection type. `Run` already carries `CapturedOutput`, `Duration`, `Error`, `UserTags`. JSON snapshots can rely on a stable shape because `Run`'s properties are stable.

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

Registered in `serializers/this.cs` constructor alongside `Json`, `Text`, etc. `file.save` dispatches by extension.

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
purge-systemio-from-actions          // currently in flight; finishes first
        │
        ├─→ typed-action-returns      // 12 mechanical + Response record + builder annotation work
        │
        └─→ test-report-typed-object  // builds on both; needs Report + JunitSerializer
```

`typed-action-returns` and `test-report-typed-object` can run in parallel after the system.io purge — `test-report` doesn't touch the 12 mechanical handlers and vice versa. `test.report` is excluded from `typed-action-returns`'s scope so the deeper rework happens cleanly on its own branch.

## Verification recipe

After each typing change in A.1:

```bash
cd /workspace/plang/Tests
rm -rf <relevant>/.build
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build '--build={"cache":false}'
```

Inspect the trace's "Variables in scope" snapshot for a downstream step that uses the variable — should now show `%var%(<T>)` instead of `%var%(object)`. The `.pr`'s trailing `variable.set` should carry `type:"<T>"`.

Across the `Tests/` corpus, the 729 `(object)` occurrences in Category A should disappear or move into Category B (where the genuine polymorphism stays honest).

## Open notes for plan refinement

1. **JUnit extension dispatch.** `.junit.xml` is two extensions; `GetByExtension` currently keys on one. Either extend the registry to walk multi-segment extensions, or pick `.xml` and disambiguate by content shape, or pick `.junit` (uglier). Lean: extend the registry — one-line change to make `GetByExtension` walk multi-segment.
2. **`builder.types` / `actions` / `goals` records** — coder reads the current implementations and names them; OBP-singular folders under `app/builder/`.
3. **`mock.intercept`** location — flagged in the original survey; coder locates and types.
4. **`test.tag`** is marginal — coder's discretion. Worth it if it's a one-liner.
5. **Persisting `castType` into `.pr`** — deferred. Pure compile-time for v1.
6. **CLAUDE.md proposal** — file under `.bot/typed-action-returns/claude-md-proposals.md`: the "Key Files" section still references `PLang/Runtime2/Engine/Utility/TypeMapping.cs`; the file is now at `PLang/app/types/this.cs ClrFromMime`.
