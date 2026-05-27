# docs v1 — typed-action-returns — result

## CHANGELOG entry (this branch, user-visible)

No repo-root `CHANGELOG` file exists. Recording the user-visible surface here so it can be lifted into release notes when the v0.2 changelog gets reconstituted.

### `typed-action-returns`

**Action shape**

- New optional compile-time hook on every action handler: `IClass.Build()`. Default impl does nothing; override to stamp a `Type` on the step's terminal `variable.set` based on literal arguments (e.g. `file.read` inferring `csv` from a literal path, `llm.query` inferring `json` from a schema). `IClass.SetAction(action, context)` is source-generator-emitted as the priming seam for `Build()`.
- `Run()` returns are now typed by signature. `Task<Data<T>>` for concrete `T`; `Task<Data<object>>` for genuinely polymorphic; bare `Task<Data>` for void-like (no `→ returns` line in catalog).

**User-facing PLang syntax**

- `(type)` hint on a write target: `- ask llm what... write to %answer%(json)` stamps `Type="json"` on the terminal `variable.set`. User hint wins over `Build()` inference.
- `output.ask` now returns `Ask` (structured) — `%name.Answer%` is the explicit accessor; `%name%` continues to read the answer in string contexts via `Ask.ToString()`.
- `mock.intercept` returns a `Mock` handle (was `MockHandle`). PLang catalog name unchanged.

**Module surface**

- `http.request` / `http.upload` return a `Response` record: `Status`, `Headers`, `Body` (dispatched by Content-Type), `Duration`. Legacy `%response.StatusCode%` / `%response.Body%` still reachable for back-compat.
- `Serializers/ISerializer` returns `Data` / `Data<T>` end-to-end. Throw list narrowed to `JsonException`, `NotSupportedException`, `IOException` — every other exception type propagates.
- `Serializers.GetByExtension` walks multi-segment extensions (`.junit.xml` → falls back to `.xml`).
- `path.Extension` no longer carries the leading dot (`"csv"`, not `".csv"`).

**Internal renames** (no PLang catalog impact)

- `app.modules.Schema.@this` → `app.modules.builder.Types.@this`. `[PlangType("catalog")]` dropped.
- `app.modules.mock.types.MockHandle` → `app.mock.Mock.@this`.
- `app.tester.File` → `app.tester.Test.@this`.

## Documentation updates landed in this pass

| File | Action | What changed |
|---|---|---|
| `Documentation/v0.2/good_to_know.md` (§ *Mock Module Architecture*) | updated | `MockHandle` references → `Mock.@this`; added rename callout |
| `Documentation/v0.2/good_to_know.md` (§ *OBP Smell #7 worked example*) | updated | `app.tester.File` → `app.tester.Test.@this` (with branch-attribution callout) |
| `Documentation/v0.2/architecture.md` (line 221) | updated | `app.modules.Schema.@this.Build()` → `app.modules.builder.Types.@this.Build()` |
| `Documentation/v0.2/good_to_know.md` | created | *Build()-time type stamping — `IClass.Build()`, `(type)` hints, and `BuildWarning`* |
| `Documentation/v0.2/good_to_know.md` | created | *`Serializers/ISerializer` returns `Data` — no throws* |
| `Documentation/v0.2/good_to_know.md` | created | *Multi-segment serializer extension matching* |
| `Documentation/v0.2/good_to_know.md` | created | *`IExitsGoal.ShouldExit()` — Value-side opt-out for resolved sentinels* |
| `os/system/modules/mock/action.description.md` | renamed | → `intercept.description.md` (was orphan; `MarkdownTeaching.ScanOrphans` would warn). Body sharpened to mention the `Mock` handle. |

## XML doc coverage on new public surface

Verified clean — every new public type carries substantive `///` docs explaining what and why:

- `IClass.Build()` / `IClass.SetAction()` — `PLang/app/modules/IClass.cs`
- `BuildWarning` record — `PLang/app/modules/builder/warning/this.cs`
- `Response` record — `PLang/app/http/Response/this.cs`
- `Test.@this` — `PLang/app/tester/Test/this.cs` (per-property docs too)
- `Mock.@this` — `PLang/app/mock/Mock/this.cs`
- `Ask` + `ask` action — `PLang/app/modules/output/ask.cs`
- `IExitsGoal` — `PLang/app/IExitsGoal.cs`
- `Channel(name)` registry method — `PLang/app/channels/this.cs:99-112`

No flag-back to coder.

## PLang examples

All renamed/typed actions have `module/<action>.examples.md` already in place. No example gaps. (Test corpus: 221/221 PLang green per tester v2.)

## Proposals processed

| Source | Count | Notes |
|---|---|---|
| `.bot/typed-action-returns/claude-md-proposals.md` | 0 | file absent |
| `.bot/typed-action-returns/character-proposals.md` | 0 | file absent |
