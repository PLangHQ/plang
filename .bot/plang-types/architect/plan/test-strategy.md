# Test strategy — `plang-types`

**Scope.** The four integration cuts below are the contract for end-to-end behavior (build→`.pr`→runtime, across channels, with runtime loading); per-topic behaviors and negative paths sit beneath them in [test-coverage.md](test-coverage.md).

## Test layer mapping

The rule: **C# TUnit pins the type's internal behavior and the build/dispatch machinery; PLang `.goal` pins the developer-facing build-and-run surfaces.** Per-behavior assignment is in the [test-coverage.md](test-coverage.md) matrix; the split in one breath:

- **C# TUnit** — `number` arithmetic / equality (lenient *and* exact) / parse / `Build`→kind / promotion table / overflow + divide behavior; the `TypeSerializers` lookup (specific ?? `"*"`); the `Normalize` tag-hook (registered → `TypedValueNode`, unregistered → reflection); `IWriter.Format`; registry fold (no-regression on `Get`/`IsPrimitive`/`Conversion`); the cleanup types' parse/round-trip. These are C#-internal — TUnit (`dotnet run --project PLang.Tests`, recompiles in place; immune to the stale-binary trap).
- **PLang `.goal`** — `set %x% = 3.5` stamps `number`/kind `decimal` in the `.pr`; `read photo.png` → `%photo%(image)`; `write out %photo%` renders per active channel; arithmetic via goals (`%a% + %b%`); `%photo.Path.Exists%` navigation; the catalog/scope showing type+kind. Run from `Tests/` (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`), after a clean rebuild (stale-binary trap — see `/CLAUDE.md`).
- **Integration cuts** — the four below; each is a `.goal` (or goal + C# harness) that exercises build and runtime together and asserts the trace.

## Integration cuts

**Cut 1 — literal kind → arithmetic → output (the type+kind spine).**
A goal: `set %x% = 1` · `set %z% = 3.5` · `set %b% = %x% + %z%` · `write out %b%`. Build it; inspect the `.pr`: `%z%` carries `type:"number", kind:"decimal"` as **separate fields** (not `"number:decimal"`), `%x%` kind `int`, the `+` step compiles to `math.add` whose result `%b%` is `type:"number"` with **no kind** (runtime-decided). Run it; output is `4.5`... (use `%x%=1, %z%=3.5` → `4.5`; pick values that prove decimal precision, e.g. `0.1 + 0.2` asserting the documented result). Proves: `Build`→kind, separate fields, the literal-shape rule, polymorphic-result-has-no-kind, end-to-end arithmetic.

**Cut 2 — same value, two channels, two wire shapes (the dispatch).**
A goal that produces an `image` (`read photo.png, write to %photo%`) then `write out %photo%`. Run once with the text serializer active (assert a path placeholder / non-base64), once with the json/wire serializer active (assert base64). Proves: `Data.Type` rides through untouched, the channel never branches on type, the `(image, format)` serializer file is selected by the writer's `Format`, the same instance renders two ways.

**Cut 3 — composition navigation (`%photo.Path.Exists%`).**
A goal: `read photo.png, write to %photo%` · `if %photo.Path.Exists% then write out "found"`. Build (the catalog must let the LLM navigate `image → Path(path) → Exists`) and run (a present file → true; point at a missing file → false). Proves: typed-property catalog, composition over union (one `image` with a `path` facet), nullable `Path` handling. A second variant on a base64-constructed image asserts `%photo.Path%` is null without crashing.

**Cut 4 — runtime type-loading + overwrite precedence.**
Load a tiny test DLL exposing a `[PlangType]` class + an `ITypeRenderer`; a goal `- load test-types.dll` · `set %x% = <value of the loaded type>` · `write out %x%`. Assert the loaded type resolves by name and renders via its registered renderer. A second variant overrides an existing name and asserts runtime registration wins (`ResolveType` precedence). Proves: `RegisterRuntime` (registry + dispatch), the `code.load`-style scan, overwrite precedence, and the typed load-failure when a loaded type ships no covering renderer.

## What these cuts don't cover (the matrix picks up beneath)

- Per-kind arithmetic: `int+int→int`, `int+decimal→decimal`, `decimal×double→` precision fork, integer-overflow `Promote` widening vs `Throw`, `7/2→3.5`, `math.intdiv`, `2^-1`, NaN falsy.
- Lenient-vs-exact equality edge cases (`0.1==0.1` true; the documented non-transitivity; `ExactEquals` distinguishing decimal/double; NaN never equal).
- Negative paths (the failure matrix): unknown type name, missing serializer coverage (PLNG build gate), parse failure → `Data.Error` (not throw at the handler), narrowing overflow → typed error, divide-by-zero.
- Per-cleanup-type round-trip: `datetime`→DateTimeOffset, `date`→DateOnly, `time`→TimeOnly, `duration`→TimeSpan (+ `timespan` alias still resolves).
- The registry-fold no-regression sweep (every old `Get`/`IsPrimitive` answer unchanged).
- `path` as first-mover: a `path` value serializes identically before/after the dispatch migration.
