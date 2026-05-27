# coder handoff — `typed-action-returns` complete

**State at handoff:** branch builds clean, full PLang.Tests suite green (3123/3123), PLang test corpus green (208/208 + 12 stale placeholders), origin at `901d3da95`.

## What shipped

### Stage 0 — Foundation infrastructure (30 tests)

- `IClass.Build()` — optional compile-time hook on every action handler. Default impl returns `Data.Ok()`.
- `IClass.SetAction(action, context)` — source-generator-emitted seam so the validate pass can prime `__action`/`__app` before invoking Build(). Mirrors ExecuteAsync's setup minus the runtime-only steps.
- `builder.code.Default.Validate` runs a per-action Build() pass. `Data.Ok(typeName)` stamps the typeName onto the step's terminal `variable.set` "Type" parameter; `Data.Fail` aborts.
- `Channel(name)` lookup with no-op fallback (`app.channels.channel.noop.@this`) — callers write opportunistically without null-checking.
- `BuildWarning` record at `app/modules/builder/warning/this.cs`.
- `[PlangType]` kept as slim Name-only override. Used only on `GoalCall` (`"goal.call"` — dotted name can't be derived). All other usages dropped or rely on class-name / @this-segment derivation.
- Catalog metadata moved from `[PlangType(Shape=, Example=, Description=)]` to a static-property convention (`public static string Example => "...";`).
- `Data.As<T>` made internal; public coercion is `Data.As(string typeName)`.

### Stage 1 — `tester.File` → `tester.Test.@this` (17 tests, 11 of which are regression coverage)

- `PLang/app/tester/File.cs` → `PLang/app/tester/Test/this.cs`. Class shape unchanged (4 own fields, PrPath/Hash/BuilderVersion still on Goal).
- Catalog name derives to `"test"` via @this convention.
- `Run.File` property name **kept as-is** (didn't rename to `Run.Test`) to limit blast radius. Property type updated.

### Stage 2 — Mechanical typings (34 tests)

| Action | New `Run()` return type |
|---|---|
| `test.discover` | `Task<Data<List<Test.@this>>>` |
| `test.run` | `Task<Data<Results>>` |
| `output.ask` | `Task<Data<Ask>>` — Ask gained `Answer` field + virtual `IExitsGoal.ShouldExit()` (false when Answer bound) + `ToString() => Answer ?? ""` |
| `channel.set` | `Task<Data>` (already bare; satisfies void-like) |
| `mock.intercept` | `Task<Data<app.mock.Mock.@this>>` (rename from `app.modules.mock.types.MockHandle`) |
| `builder.types` | `Task<Data<app.builder.Types.@this>>` (rename from `Schema.@this`; `[PlangType("catalog")]` dropped) |
| `builder.actions` | `Task<Data<StepActions>>` (no wrapper — preserves PLang's `foreach %actions%`) |
| `builder.goals` | `Task<Data<List<Goal>>>` (same — preserves `%goals.Count%`) |
| `goal.getTypes` | `Task<Data<List<Dictionary<string,string>>>>` (same — preserves `%varTypes[step.Index]%`) |
| `test.tag` | `Task<Data>` bare (low-priority; never degrades to `Data<object>`) |

### Stage 3 — HTTP Response + Content-Type dispatch (23 tests)

- New record at `app/http/Response/this.cs`: `record @this(int Status, Dictionary<string,string> Headers, object? Body, TimeSpan Duration)`.
- `http.request` / `http.upload` typed `Task<Data<Response>>`. `http.download` unchanged (writes to disk; signature still `Task<Data>`).
- Response.Body dispatched by Content-Type. After the later Serializers refactor (below), the dispatch flows through `Serializers.GetByContentType` + a `TextFallback` for text-shaped misses (text/*, xml, json, csv) and byte[] for binary.
- Legacy `BuildProperties` bag kept populated so existing PLang code reading `%response.StatusCode%` still works alongside the new `%response.Status%` / `%response.Body%`.

### Stage 4 — Per-action Build() + (type) hint + multi-segment serializer (30 tests)

- `file.read.Build()` — literal Path → infer type from `path.Extension` via `Formats.Mime`. Missing literal files emit a BuildWarning on `Channel("builder")` but still return the inferred type.
- `llm.query.Build()` — Schema set → `Ok("json")`; Format set → `Ok(Format)`; else `Ok()`.
- `http.request.Build()` / `http.upload.Build()` — literal URL with recognized extension → infer type; query/fragment stripped before scan.
- `Serializers.GetByExtension` walks multi-segment extensions (`.junit.xml` → `.xml`) with fallback to the trailing segment.
- `Compile.llm` kernel rule: `write to %answer%(json)` → LLM emits `Type="json"` on the terminal `variable.set`.
- Precedence: user (type) hint wins over Build() inference. Any explicit `Type` on the variable.set (including literal `"object"`) is treated as a user hint.

### Bonus: `Serializers/ISerializer` return `Data`

- Every `ISerializer` method returns `Data` / `Data<T>` instead of bare T?.
- Impls (Json, Text, plang, plang+data) wrap each method body in try/catch over JsonException/NotSupportedException/IOException and return Data.FromError.
- Registry methods propagate Data through (`Deserialize<T>` returns `Data<T>`, `DeserializeAsync<T>` returns `Task<Data<T>>`, `SerializeAsync` returns `Task<Data>`).
- Call sites updated to check Success and read `.Value`/`.Error` instead of try/catch.

### Bonus: http body dispatch through registry

- `ParseResponseAsync` no longer hardcodes if/else over Content-Type. It reads bytes, asks the registry for a serializer by Content-Type, deserializes if one's found (falling back to TextFallback on parse failure), and falls back to text-or-bytes when no serializer is registered.
- Drops the predates-Serializers-registry `JsonSerializer.Deserialize` + `JsonException` catch.

### Bonus: `path.Extension` no leading dot

- `PathHelper.GetExtension` now returns `"csv"` not `".csv"`. Every existing caller trimmed the dot anyway; `Formats.Mime` normalises it back on if needed.

## Decisions locked with Ingi during the work

These are real architectural calls, not implementation details. Surface them in the PR description.

1. **`output.ask` → `Task<Data<Ask>>`.** Ask carries `Answer`; `IExitsGoal.ShouldExit()` is virtual and Ask returns false when Answer is bound. PLang code that writes `output.ask, write to %name%` previously got a string; now gets an Ask. `Ask.ToString() => Answer ?? ""` so common PLang patterns (`%name% equals "Alice"`, string interpolation) still work without changes. `%name.Answer%` is the explicit structured accessor. Call sites in `path/this.Authorize.cs` and `path/file/this.Operations.cs` extract `Ask.Answer`.

2. **`Data.Warnings` stays.** Surfaced as a smell during the refactor (`builder.code.Default.Goals` is the only attachment site, the only consumer is `Data<T>.From`'s forward path). Ingi's call: keep it.

3. **`builder.actions` / `builder.goals` typed to natural collection shapes, not wrapper records.** PLang's `Build.goal` does `foreach %goals%, call BuildGoal` and `%goals.Count%`. A wrapper record would have required `%goals.Goals%` everywhere with no observable benefit.

4. **`Schema.@this` renamed to `builder.Types.@this` (full folder move).** Dropped `[PlangType("catalog")]` — the @this segment "Types" derives cleanly to "types". Reduced named-`[PlangType]` sites from 2 to 1 (only GoalCall left).

5. **`MockHandle` renamed to `app.mock.Mock.@this`.** No PLang catalog name change (derives to "mock" via @this). MockCall stayed as a sibling Call class in the same folder.

## What's intentionally deferred (not in this branch)

- **`file.save` cross-type coercion.** Logged in `Documentation/v0.2/todos.md`. The architect's canonical end-to-end test for `Data.As(format)` materialization.
- **Branch B `test-report-typed-object`.** Separate fork off `runtime2`. Depends on Stage 0's Channel(name) + Stage 4's multi-segment GetByExtension, both of which shipped.
- **Per-property style transforms in `path.Authorize`.** Two other Type.Exit call sites (`path/this.Operations.cs:32,131,144` and `file/read.cs:29`) still use the old `Type?.ClrType.Exit()` pattern. They're handling results from other operations (file IO, copy) that don't currently return `Data<Ask>`, so they work the same as before. Switching them to `ShouldExit()` would be more semantically correct but isn't needed.
- **TestReport branch B's JunitSerializer.** Multi-segment `.junit.xml` registration mechanism is in place; the serializer itself is Branch B work.
- **`Run.File` → `Run.Test` property rename.** Type updated to `Test.@this` but property name kept as `File` to limit blast radius. Follow-up worth doing for naming consistency.

## Known-fragile bits / things to watch

- **The Data<object> implicit-operator footgun (CLAUDE.md "Data<T> implicit-operator footgun").** Every typed handler in this branch was checked. Any new typed handler must use `Data<T>.Ok(value)` or `Data<T>.From(source)` explicitly when forwarding a Data — never `return innerData;` from a `Task<Data<object>>` signature.
- **`Ask.ToString()` returns Answer.** This is a deliberate ergonomic for PLang assertion patterns. Consumers that wanted "the type name of the Value's class" via ToString now get the answer string instead. No production sites currently rely on the old behavior, but it's a sharp edge.
- **`Serializers` impls all carry try/catch around `JsonException, NotSupportedException, IOException`.** If a serializer impl throws a different exception type (out-of-memory, cancellation), it still propagates — by design. Anything else "expected" should land in the catch list.
- **`output.ask` resume path consumes `!ask.answer` and casts to string via `?.ToString()`.** The original Variables.Get returned a Data with .Value typed; the new code converts to string. If anyone pre-binds `%!ask.answer%` to a non-string value, they'll get the ToString rendering as the Answer.

## Files of interest for the reviewer

| File | Why |
|---|---|
| `PLang.Generators/Emission/Action/this.cs` | SetAction emission. The seam handlers go through to be ready for Build(). |
| `PLang/app/modules/builder/code/Default.cs` | The RunBuildPass + StampOnTerminalVariableSet helpers — the heart of the Build() inference flow. |
| `PLang/app/modules/output/ask.cs` | Ask record shape + Run() forwarding rules between stream-channel and stateless-channel. |
| `PLang/app/types/path/this.Authorize.cs` | Consumer of `Data<Ask>` — uses `ShouldExit()` to honor the Value-side opt-out. |
| `PLang/app/channels/serializers/serializer/this.cs` + impls | The Data-bearing ISerializer surface. |
| `PLang/app/modules/http/code/Default.cs` | `ParseResponseAsync` + `TextFallback` — the Content-Type dispatch through the registry. |
| `os/system/builder/llm/Compile.llm` | The `(type)` hint kernel rule the LLM reads. |

## Test counts

- `dotnet run --project PLang.Tests` — **3123/3123** ✅
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` — **208 pass, 0 fail, 12 stale** (stale = TypedReturns/* placeholder PLang test goals the test-designer authored without implementations; never built so no .pr; pre-existing, not regression)

## Commit log on this branch

```
901d3da95 coder: Ask.ToString() renders Answer for PLang string contexts
d7dbf1c01 coder: regression fixes from full PLang.Tests sweep
e1d770fd9 coder: http body dispatch flows through Serializers.GetByContentType
5b1b894c4 coder: Serializers/ISerializer return Data instead of bare T?
7b3488462 coder Stage 4: per-action Build() + (type) hint + multi-segment GetByExtension
45132b0ac coder Stage 2 done: type goal.getTypes
d3832f713 coder: type builder.actions and builder.goals to natural collection shapes
cc4f2fc67 coder Stage 2a: typed test.discover + test.run
312e94e7a coder Stage 1: rename tester.File → tester.Test.@this
3c8285760 coder Stage 0 (test bodies #2-4/4): 22 tests
a4bc37999 coder Stage 0 (test bodies #1/4): BuildMethodTests 8/8
afeccfb26 coder Stage 0 (Build seam): SetAction emission + validate Build() iteration
```

(Plus the rename/scrub commits for Schema → builder.Types and MockHandle → Mock.)
