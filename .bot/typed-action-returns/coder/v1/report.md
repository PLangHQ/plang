# Coder v1 — typed-action-returns

## Version
v1 — Stages 0-4 complete + Serializers Data refactor + http body dispatch through registry

## What this is

The branch's full architectural contract is implemented and on origin (`2f1d1889b`). 134 stage tests, full PLang.Tests sweep at 3123/3123, PLang test corpus at 208 pass / 0 fail / 12 stale (pre-existing test-designer placeholders).

The prior coder ([handoff.md](../handoff.md)) shipped Stage 0 scaffolding and paused mid-stage. This pass picks up there, finishes Stages 0-4, then layers two follow-up refactors Ingi asked for.

---

## Stage 0 — Foundation infrastructure (30/30 green)

Built on the prior coder's `IClass` + noop channel + slim `[PlangType]` + `Data.As(string)` work.

**SetAction seam.** Source generator emits `public void SetAction(action, context)` on every handler partial — mirrors `ExecuteAsync`'s `__action`/`__app`/`__resolutionError` + backing-field reset + capability-prop wiring, minus the runtime-only steps (Channel resolve, IEvent, [Code] eager, IsNotNull, Run try/catch). The interface declares it so `builder.validate` calls through without reflection.

**Validate Build() iteration.** `builder.code.Default.RunBuildPass` walks each action's handler, calls `SetAction` then `Build()`. `Data.Ok(typeName)` stamps the typeName onto the step's terminal `variable.set`'s "Type" parameter via `StampOnTerminalVariableSet` (backwards walk, replace-or-insert). `Data.Fail` aborts validation. Bare `Data.Ok()` contributes nothing.

**Stage 0 test bodies.** 30 tests landed across BuildMethod / DataMaterialization / NamedChannels / PlangTypeRemoval. Two `[PlangType]`-removal tests reframed per the prior coder's handoff (attribute kept slim, not deleted — only GoalCall's `"goal.call"` divergent-name override remains).

---

## Stage 1 — `tester.File` → `tester.Test.@this` (17 tests including 11 regression)

Full folder move to OBP singular layout. Class shape unchanged (4 own fields; PrPath/Hash/BuilderVersion still on Goal). PLang catalog name derives to `"test"` via @this convention. `Run.File` property name **kept as-is** to limit blast radius; only the type was updated to `Test.@this`. Worth a follow-up to rename the property too.

---

## Stage 2 — Mechanical typings (34/34 green)

10 handlers typed. Decision shape per Ingi:

| Action | Run() return | Wrapper record? |
|---|---|---|
| `test.discover` | `Task<Data<List<Test.@this>>>` | no, natural type |
| `test.run` | `Task<Data<Results>>` | no, existing record |
| `output.ask` | `Task<Data<Ask>>` | Ask extended (see Decision 1) |
| `channel.set` | `Task<Data>` | bare, void-like |
| `mock.intercept` | `Task<Data<app.mock.Mock.@this>>` | yes — full rename from `MockHandle` |
| `builder.types` | `Task<Data<app.builder.Types.@this>>` | yes — full rename from `Schema.@this` |
| `builder.actions` | `Task<Data<StepActions>>` | **no** — wrapper would break `foreach %actions%` |
| `builder.goals` | `Task<Data<List<Goal>>>` | **no** — wrapper would break `%goals.Count%` |
| `goal.getTypes` | `Task<Data<List<Dictionary<string,string>>>>` | **no** — wrapper would break `%varTypes[idx]%` |
| `test.tag` | `Task<Data>` | bare; never degrades to `Data<object>` |

For the wrapper renames, the [PlangType("...")] override was dropped — both new homes derive cleanly via @this segment. Only `GoalCall → "goal.call"` keeps the named override (dotted name can't be derived).

---

## Stage 3 — HTTP Response record + Content-Type body dispatch (23/23 green)

New record at `app/http/Response/this.cs`:

```csharp
public sealed record @this(
    int Status,
    Dictionary<string, string> Headers,
    object? Body,
    System.TimeSpan Duration);
```

`http.request` / `http.upload` typed `Task<Data<Response>>`. `http.download` untouched (writes to disk).

Initial impl had its own Content-Type dispatch (json / xml / text/* / binary). After the bonus refactors below, it now flows entirely through `Serializers.GetByContentType` with a `TextFallback` for text-shaped misses. Duration captured via Stopwatch at SendAsync/UploadAsync entry.

Legacy `BuildProperties` bag kept populated so existing PLang code reading `%response.StatusCode%` still works alongside the new `%response.Status%` / `%response.Body.field%`.

---

## Stage 4 — Per-action Build() implementations + (type) hint + multi-segment GetByExtension (30/30 green)

`file.read.Build()` reads `Path.Value.Extension` (post-`PathHelper.GetExtension` no-dot rework) + `path.MimeType`. Literal `foo.csv` → `Ok("csv")`. Variable refs and unknown extensions → `Ok()`. Missing literal files emit a BuildWarning on `Channel("builder")` but still return the inferred type — non-fatal.

`llm.query.Build()` — Schema set → `Ok("json")`; Format set → `Ok(Format)`; else `Ok()`.

`http.request.Build()` / `http.upload.Build()` — literal URL with recognized extension → infer; query/fragment stripped before scan via shared `HttpBuildHelpers.InferTypeFromUrl`.

`Serializers.GetByExtension` walks multi-segment extensions (`.junit.xml` → `.xml`) with fallback to the trailing segment.

`Compile.llm` kernel: new "(type) hint on a write target" rule with worked example. `write to %answer%(json)` → LLM emits `Type="json"` on the trailing `variable.set`.

Precedence: `StampOnTerminalVariableSet` only stamps when no `Type` parameter is present. Any explicit Type the LLM emitted (including literal `"object"`) wins as a user (type) hint.

---

## Bonus: `Serializers/ISerializer` return `Data`

Triggered by Ingi spotting the smell — every method on the serializer surface returned bare `T?` (parse errors as exceptions, null overloaded between "legitimate null result" and "parse failure").

Every method on `ISerializer` now returns `Data` / `Data<T>`:

- `SerializeAsync(stream, ...)` → `Task<Data>`
- `DeserializeAsync(stream, type, ...)` → `Task<Data>`
- `DeserializeAsync<T>(stream, ...)` → `Task<Data<T>>`
- `Serialize(value, type)` → `Data<string>`
- `Deserialize(string, type)` → `Data`
- `Deserialize<T>(string)` → `Data<T>`

Impls (Json, Text, plang, plang+data) wrap each body in try/catch over `JsonException`, `NotSupportedException`, `IOException` → `Data.FromError` with a typed ServiceError. Registry methods propagate through. 8 production call sites + 2 test stubs + ~30 test assertions updated.

---

## Bonus: http body dispatch through registry

Triggered by Ingi spotting that my Stage 3 ParseResponseAsync still hardcoded `if (contentType.Contains("json"))` while the Serializers registry could do that dispatch directly. Shape now:

```csharp
var bytes = await ReadLimitedBytesAsync(...);   // size cap intact
var serializer = Serializers.GetByContentType(contentType);
if (serializer != null) {
    using var stream = new MemoryStream(bytes);
    var deser = await serializer.DeserializeAsync(stream, typeof(object));
    body = deser.Success ? deser.Value : TextFallback(bytes, contentType);
} else {
    body = TextFallback(bytes, contentType);
}
```

`TextFallback` picks utf-8 string for text/xml/json/csv content types, byte[] for everything else.

`ReadLimitedStringAsync` is no longer needed on this path — `ReadLimitedBytesAsync` + `Encoding.UTF8.GetString` covers what it did. The 100MB size cap on the network read stays where it always was (transport security concern, not serializer concern).

Drops the predates-Serializers-registry direct `JsonSerializer.Deserialize` + bespoke `JsonException` catch.

---

## Bonus: `path.Extension` no leading dot

Triggered by Ingi spotting the call-site smell — every caller of `path.Extension` immediately did `.TrimStart('.')`. Now `PathHelper.GetExtension` returns `"csv"` not `".csv"`; `Formats.Mime` normalises it back on if needed; consumers stop trimming.

---

## Decisions locked with Ingi

### Decision 1: `output.ask` returns `Task<Data<Ask>>`, Ask carries `Answer`

The architect's plan had `output.ask` returning `Task<Data<string>>` but the IExitsGoal Ask sentinel (used for stateless-channel suspend) couldn't flow through that signature without losing the suspend mechanism. I asked Ingi: split IExitsGoal out, or downgrade to bare `Task<Data>`?

His call: **extend Ask with an `Answer` field**, make `IExitsGoal.ShouldExit()` a virtual method (default true), have Ask override to `Answer == null`. Resume path returns `Data<Ask>.Ok(new Ask { Answer = userResponse })`; suspend path returns `Data<Ask>.Ok(new Ask())`. `Data.ShouldExit()` checks Value-side first, then Type-side, so a resolved Ask flows through while a pending Ask short-circuits.

PLang side: previously `output.ask, write to %name%` bound a string. Now binds an Ask. Added `Ask.ToString() => Answer ?? ""` so existing PLang patterns (`%name% equals "Alice"`, string interpolation) keep working. `%name.Answer%` is the explicit structured accessor for code that wants the typed shape.

Two C# call sites that consumed `askResult.Value as string` (path.Authorize and file Operations bundle) updated to extract `Ask.Answer` and use `ShouldExit()` instead of the Type-only Exit check.

### Decision 2: `Data.Warnings` stays

Surfaced as a smell during the refactor — only one attachment site (`builder.code.Default.Goals`), one forward (`Data<T>.From`). Ingi's call: keep.

### Decision 3: `builder.actions` / `builder.goals` typed to natural collection shapes

Architect's plan called for new wrapper records at `app/builder/Actions/this.cs` and `app/builder/Goals/this.cs`. I created them, then realised PLang's `Build.goal` does `foreach %goals%, call BuildGoal goal=%item%` and `write out "Found %goals.Count% goals"`. Wrapping behind a record would force `%goals.Goals%` and `%goals.Goals.Count%` everywhere with no observable benefit. Dropped the wrappers, typed to the natural shape. Same for `goal.getTypes` (`%varTypes[step.Index]%` keeps working).

`builder.types` got the full wrapper rename (`Schema → builder.Types.@this`) because it's a real entity, not just a collection.

### Decision 4: Schema → builder.Types full folder rename + drop `[PlangType("catalog")]`

`app/modules/Schema/` (all 5 files) → `app/builder/Types/`. Namespace `app.modules.Schema` → `app.builder.Types`. The @this convention derives `"types"` cleanly, so the override was dropped. Reduces named-`[PlangType]` sites from 2 to 1 — only `GoalCall → "goal.call"` (dotted name, can't be derived) remains.

### Decision 5: `MockHandle` rename to `app.mock.Mock.@this`

Following the same OBP-singular pattern. `MockCall` stays as a sibling `Call` class in the new folder. Catalog name derives to `"mock"` via @this convention; no `[PlangType]` override needed.

---

## Pre-handoff sweep findings

Three regression categories the full PLang.Tests sweep caught (now all fixed):

- **`path.Authorize` was using `Type.ClrType.Exit()`** which fired on every `Data<Ask>` including resolved ones (Type=ask is always IExitsGoal). Switched to `ShouldExit()` (honors Value-side opt-out). Also `path/file/this.Operations.cs:381` had the same pattern.

- **`OpenAi` LLM provider** was passing `httpResult.Value` (now a `Response` record) to `ParseApiResponse` which expects the JSON body. Unwrap via `(httpResult.Value as Response.@this)?.Body` first.

- **`Stage2_StreamChannelTests`** asserted error key `"WriteError"`. After the Serializers refactor, serializer errors travel directly through `Data.Error` without the channel wrapping — the key is now `"TextSerializeError"`. Test pinned to the new specific key.

PLang test corpus: 3 Callback/Ask tests failed because `%name%` is now an Ask. Fixed by adding `Ask.ToString()`.

---

## Test status at handoff

- **`dotnet run --project PLang.Tests`** — 3123/3123 ✅
- **`cd Tests && plang --test`** — 208 pass, 0 fail, 12 stale, 0 timeout
  - Stale: the 12 `TestModule/TypedReturns/*` placeholder PLang test goals the test-designer authored without implementations; never built so no .pr. **Pre-existing**, not a regression from this branch.
- **`plang build`** fails locally because the dev machine has no LLM config. Environmental, not a code regression.

---

## What's intentionally deferred

| Item | Where it lives |
|---|---|
| `file.save` cross-type coercion (canonical end-to-end test for `Data.As(format)`) | `Documentation/v0.2/todos.md` |
| Branch B `test-report-typed-object` (depends on Stage 0's Channel(name) + Stage 4's multi-segment GetByExtension — both shipped) | separate branch off `runtime2` |
| `Run.File` → `Run.Test` property rename (type updated, name kept for blast-radius) | follow-up |
| Other `Type?.ClrType.Exit()` call sites (`path/this.Operations.cs:32,131,144`, `file/read.cs:29`) | Could switch to `ShouldExit()` for semantic consistency, but they handle results from other operations (file IO, copy) that don't currently return `Data<Ask>`, so they work the same as before. |
| Branch B's JunitSerializer impl (multi-segment registration mechanism is in place) | Branch B |
| `error.handle.cs:7` `try { body }` — bespoke try/catch around `_caseInsensitiveRead` json parse in error-body decoding (4KB cap, low-traffic path) | not worth touching in this scope |

---

## Known-fragile bits for the next reviewer

1. **`Data<object>` implicit-operator footgun** (CLAUDE.md "Data<T> implicit-operator footgun"). Every typed handler in this branch was checked. Any new typed handler must use `Data<T>.Ok(value)` / `Data<T>.From(source)` explicitly when forwarding a Data — never `return innerData;` from a `Task<Data<object>>` signature.

2. **`Ask.ToString()` returns `Answer`.** Deliberate for PLang assertion patterns. Consumers that wanted "the type name of the Value's class" via ToString now get the answer string. No production sites currently depend on the old behaviour, but it's a sharp edge.

3. **All `Serializers` impls carry try/catch around `{JsonException, NotSupportedException, IOException}`.** A different exception type (OOM, cancellation) still propagates by design. Anything else "expected" needs adding to the catch list.

4. **`Default.cs` http `TextFallback` content-type list is hardcoded** (`text/*`, `xml`, `json`, `csv`). A new media type that should fall back to string needs adding here.

5. **`output.ask` resume path consumes `!ask.answer` and `?.ToString()`s the value.** Pre-bound non-string values would render as their ToString form.

6. **The 12 stale TypedReturns/ PLang test goals** are the test-designer's PLang-side contract that was never built. They're placeholders that would exercise the `(type)` hint and Build()-inference end-to-end through real PLang code. Worth scoping into a follow-up PR (would need an LLM build pass — env-specific).

---

## Files of interest

| File | Why |
|---|---|
| `PLang.Generators/Emission/Action/this.cs` | SetAction emission. The seam handlers go through to be ready for Build(). |
| `PLang/app/modules/builder/code/Default.cs` | RunBuildPass + StampOnTerminalVariableSet — the Build() inference flow. |
| `PLang/app/modules/output/ask.cs` | Ask record shape + Run() forwarding rules between stream and stateless channels. |
| `PLang/app/types/path/this.Authorize.cs` | Consumer of `Data<Ask>` — uses `ShouldExit()` to honor Value-side opt-out. |
| `PLang/app/channels/serializers/serializer/this.cs` + 4 impls | Data-bearing ISerializer surface. |
| `PLang/app/modules/http/code/Default.cs` `ParseResponseAsync` + `TextFallback` | Content-Type dispatch through the registry. |
| `os/system/builder/llm/Compile.llm` | The `(type)` hint kernel rule the LLM reads. |
| `.bot/typed-action-returns/coder/handoff.md` | The prior coder's mid-flight handoff. Context for the architectural calls already locked at Stage 0. |

---

## Commit log on this branch (15 commits)

```
2f1d1889b coder handoff: typed-action-returns complete
901d3da95 coder: Ask.ToString() renders Answer for PLang string contexts
d7dbf1c01 coder: regression fixes from full PLang.Tests sweep
e1d770fd9 coder: http body dispatch flows through Serializers.GetByContentType
5b1b894c4 coder: Serializers/ISerializer return Data instead of bare T?
7b3488462 coder Stage 4: per-action Build() + (type) hint + multi-segment GetByExtension
45132b0ac coder Stage 2 done: type goal.getTypes
d3832f713 coder: type builder.actions and builder.goals to natural collection shapes
e5cfe81d4 coder Stage 2b: type output.ask as Task<Data<Ask>>; Ask carries Answer
cc4f2fc67 coder Stage 2a: typed test.discover + test.run; partial test bodies
5d7e71d98 coder: rename Schema → builder.Types; drop [PlangType("catalog")]
312e94e7a coder Stage 1: rename tester.File → tester.Test.@this
3c8285760 coder Stage 0 (test bodies #2-4/4): Data materialization, channels, PlangType — 30/30 green
a4bc37999 coder Stage 0 (test bodies #1/4): Stage0_BuildMethodTests green (8/8)
afeccfb26 coder Stage 0 (Build seam): SetAction emission + validate Build() iteration
```
