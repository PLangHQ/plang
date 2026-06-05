# Coder report v1 — `scalars-as-native`

## Summary

Built the value-lattice foundation: the `item` apex and all eight scalar value
wrappers, each `: item.@this` with full behavior (compare / equality / truthiness
/ parts / ops / bare-render) and isolated C# unit tests. ~40 new C# tests pass;
the full C# suite shows **no regressions** (one pre-existing test legitimately
updated — see below). Committed as green, per-stage increments.

The **lock phase** (born-native construction flip + the ~197-site `is string`
sweep + the `where T : item` constraint + ScalarComparer collapse + coercion
mediator rewrite) is **deferred** — it is deeply interdependent and the PLang
integration-test net is currently unavailable (see Blocker). 23 lock-phase C#
stubs remain failing by design; they are the contract for the next pass.

## Delivered (committed, C#-verified green)

| Stage | What landed |
|---|---|
| 1 | `app/type/item/this.cs` — apex + un-narrowed type. Storage-free; carries only the universal contract: sync `IsTruthy()` + `IBooleanResolvable.AsBooleanAsync` default + no-op `Narrow()`. Does **not** implement `IOrderableValue`/`IEquatableValue`. `number`/`dict`/`list` now `: item`; their truthiness moved to `override IsTruthy()`. `dict : item` still throws on `Compare.Order`. |
| 2 | `text.@this` built out `: item` — ops (length/case/trim/contains/startsWith/endsWith/indexOf/substring/replace/split→`list<text>`), ordinal case-insensitive `Order`+`AreEqual` (matches historical ScalarComparer), value `Equals`/`GetHashCode`, empty-falsy, non-`IEnumerable` (atomic). |
| 3 | `datetime.@this` built out (DateTime ctor, parts, instant compare/equality, bare ISO). **New** `date.@this` (DateOnly) and `time.@this` (TimeOnly) as their own types — date no longer collapses into datetime; time no longer unhandled. |
| 4 | `duration.@this` built out — parts, length compare, value equality, zero-is-falsy (documented), bare ISO-8601. |
| 5 | **New** `bool.@this` `: item` — the truthiness primitive (`IsTruthy` returns the raw bool), equality-only (`Order` throws), bare `true`/`false`. |
| 6 | **New** `null.@this` singleton `: item` — always falsy, `null==null` only, bare `null`. 4 isolation tests pass; 2 Data-integration tests (Data.Null() stamps singleton, sorts-last via Compare) stubbed for the flip. |
| — | `this.Convert.cs` for date/time/bool (mirror datetime/duration) so the new name→wrapper duality is conversion-safe. |

Each wrapper declares `OwnedClrTypes`. C# test files in
`PLang.Tests/App/ScalarsAsNative/`: ItemApexTests, NumberRegressionTests,
TextWrapperTests, DateTimeWrapperTests, DateWrapperTests, TimeWrapperTests,
DurationWrapperTests, BoolWrapperTests, NullWrapperTests (4/6).

## Regression caught + fixed

Adding `bool.@this` (and date/time) made the type registry resolve the name
`bool` to `app.type.bool.@this` — the same name/@this-class duality
`text`/`number`/`datetime` already carry. This broke
`TypeEntityHomeTests.DataType_OnStampedData_ResolvesViaAppTypeIndexer`, which had
deliberately used `bool` as a "clean 1:1 primitive." Since bool is now a domain
type **by design**, the test was switched to `guid` (no wrapper). Conversion
stays correct because the new `Convert` hooks yield raw CLR values (and the
`OwnerOf` fallback already handled the raw target).

## Deferred — the lock phase (23 stubs)

Why deferred, not done: these are interdependent and need the integration net.

1. **Born-native construction flip** (`UnwrapJsonElement` String→text, True/False→bool,
   Null→Data.Null() singleton; `variable.set`/CLI/action results) — once flipped,
   every `is string`/`is bool`/… in handler bodies goes silently false. Stubs:
   `UnwrapJsonElement_*`, `NoRawScalarEscapes_ParseSeamSweep`,
   `UnwrapNewtonsoftToken_IsDeleted`, the born-native PLang goals.
2. **The ~197-site body sweep** — behavioral→method, perimeter→`.Value`,
   coercion→mediator. Must land *with* the construction flip per type.
3. **`where T : item` constraint.** Blocked on a real refactor: **`Variable` is a
   `record` and `item.@this` is an abstract `class` — a record cannot inherit a
   plain class.** Turning the constraint on requires either making `item` a record
   (impossible — the class wrappers inherit it) or converting `Variable`
   record→class, plus making `path`/`image`/`code`/`Ask`/`snapshot` `: item`
   (the IBooleanResolvable methods become `override`), plus threading
   `where T : item` through ~25 generic `Data<T>` methods. Stubs:
   `Constraint_*`, `Variable_IsItem`, `AskSnapshotPath_AreItem`.
4. **ScalarComparer collapse + coercion mediator rewrite** (`NormalizeTypes`
   inspects wrappers). Stubs: `ScalarComparer_*`, `Compare_OrderText`, `Mediator_*`,
   `ToBoolean_RawScalarFallbacks`.
5. **Per-wrapper serializers in Normalize** (bare render on the wire). Each wrapper
   exposes its bare form via `ToString()` today; the renderer files
   (`serializer/<format>.cs` `Write`) land with the flip.
6. **Null Data-integration** (`Null_Compare_SortsLast`, `Null_IsValueNotAbsence`).

### Recommended sequence for the next pass
Per-type, construction-flip + body-sweep together (architect's vertical cut),
text first. Land `Variable` record→class + everything-`: item` before turning on
the constraint (it is the final lock). Do it with the PLang integration suite
running so the body sweep has a net.

## Blocker — PLang integration suite unbuildable

`plang build` fails with `BuilderPlannerFailed` / empty-action steps even for
trivial goals (`set %a% = 2`) — the LLM builder is systemically unavailable in
this environment. So the `Tests/ScalarsAsNative/Stage*/` goals could not be built
or run. The C# unit suite (recompiles in-place, no LLM) is the deterministic
backstop used throughout and covers every wrapper, the apex contract, and the
caught regression. The PLang goals authored in v1/v2 by the test-designer remain
as committed source for the tester bot / a healthy builder; the Stage-1 goals
were filled with real assertions, the rest stay as the test-designer left them.

## Verification
- `dotnet build PlangConsole` — 0 errors.
- `dotnet run --project PLang.Tests` — only the 23 lock-phase stubs fail; no
  regressions across the rest of the suite.
