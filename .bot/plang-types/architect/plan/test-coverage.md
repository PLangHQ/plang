# Test coverage — `plang-types`

Heavy reference for test-designer. One test per matrix row. Read top-to-bottom. Layer = where the test lives (C# TUnit / goal / integration cut). Sense = green (works) / negative (fails correctly).

## 1. Coverage matrix

### storage.md — `number` the value type

| Behavior | Layer | Sense |
|---|---|---|
| `Build(3.5)` → kind `decimal`; `Build("3")`→`int`; `Build("3000000000")`→`long`; `Build("3e2")`→`double` | C# | green |
| `Parse`/`Resolve` narrowest-fit: `"5"`→Int, `"5.0"`→Decimal, `"5e0"`→Double | C# | green |
| `Resolve(string, context)` does **not** store context (number is Context-free) | C# | green |
| implicit-IN from int/long/decimal/float/double | C# | green |
| explicit-OUT cast lossy → throws (`(int)` on out-of-range / NaN) | C# | negative |
| lenient `==`: `From(5)==From(5L)`, `From(5m)==From(5.0)` true | C# | green |
| lenient `==` non-transitive at precision boundary (documented) — assert the documented trio | C# | green (asserts the known shape) |
| `ExactEquals` distinguishes `decimal(0.1)` from `double(0.1)` | C# | green |
| NaN: `From(NaN)==From(NaN)` false; NaN is falsy via `IBooleanResolvable` | C# | green |
| `GetHashCode` canonical: `From(5)`,`From(5L)`,`From(5m)` share a bucket | C# | green |
| `serializer/Default.cs` emits the right `IWriter` primitive per Kind | C# | green |
| `set %x% = 3.5` → `.pr` `{type:"number", kind:"decimal"}` as separate fields | goal | green |
| immutability — no public setters / readonly slots | C# | green |

### policy.md — arithmetic + policy

| Behavior | Layer | Sense |
|---|---|---|
| promotion: int+int→int, int+long→long, int+decimal→decimal, anything+double→double | C# | green |
| `decimal × double` precision fork: `Precision=Double`→double, `Precision=Decimal`→decimal | C# | green |
| **divide leaves integer track**: `7/2 → 3.5` (lenient) | C# + goal | green |
| `math.intdiv`: `7 intdiv 2 → 3` | C# + goal | green |
| power: `2^10→1024`, `2^-1→0.5`, `2^0.5→double` | C# | green |
| overflow `Promote`: int overflow widens to long, long→decimal | C# | green |
| overflow `Throw`: int+int past int → typed error (not silent) | C# | negative |
| policy resolves step → context → app-default → record-default via `app.config` | C# | green |
| sub-goal inherits parent context policy (walk climbs `context.Parent`) | C# + goal | green |
| `- set math.number.overflow = throw` then a math step honors it | goal | green |
| `math.add` returns `Data<number>`; overflow surfaces as `Data.Error("MathOverflow")` not an exception | C# | negative |
| `MathHelper.ToDouble`/`PreserveType` deleted — no refs remain | C# (build) | green |

### dispatch.md — serialization

| Behavior | Layer | Sense |
|---|---|---|
| `Normalize` tags a registered-type value as `TypedValueNode`; unregistered → reflection (unchanged) | C# | green |
| `TypeSerializers` lookup: specific `(type,format)` hit; miss → `(type,"*")` Default | C# | green |
| writer `TypedValueNode` case calls the looked-up `Write`; `IWriter.Format` returns the token | C# | green |
| `image` renders: text→path placeholder, json/plang→base64, protobuf→bytes (stub) | C# + integration cut 2 | green |
| `code` renders: Default→source string | C# | green |
| `path` (first mover) serializes identically before/after the dispatch migration | C# | green |
| `PLNG_SerializerCoverage`: a `[PlangType]` with neither `Default.cs` nor full coverage → build error | C# (generator) | negative |
| nested registered type (a registered value inside another) round-trips through the writer | C# | green |

### types.md — vocabulary + registration + navigation

| Behavior | Layer | Sense |
|---|---|---|
| registry fold: every prior `Get`/`IsPrimitive`/`ResolveName`/`ResolveType` answer unchanged | C# | green (no-regression) |
| `[PlangType]` discovery registers number/image/code/path | C# | green |
| typed-property catalog: `image` lists `Path(path)`; LLM can navigate `%photo.Path.Exists%` | goal + integration cut 3 | green |
| `image.Path` nullable: base64-constructed image → `%photo.Path%` null, no crash | C# + goal | green/negative |
| `read photo.png` → `%photo%(image)`; `file.read.Build()` resolves extension → high-level type | goal | green |
| cleanups: `datetime`→DateTimeOffset, `date`→DateOnly, `time`→TimeOnly, `duration`→TimeSpan round-trip | C# | green |
| `timespan` deprecated alias still resolves to TimeSpan | C# | green |
| DateTime banished — no production binding resolves to `System.DateTime` | C# | negative |

### build-vs-runtime.md — the build/runtime split

| Behavior | Layer | Sense |
|---|---|---|
| literal baked value-native (`3.5` as JSON number, not `"3.5"`); no runtime string parse | goal (inspect `.pr`) | green |
| polymorphic result (`math.add`) `.pr` has `type` but no `kind` | goal | green |
| `file.read.Run()` returns bare Data, constructs the typed value; Type/kind from build | C# + goal | green |
| runtime never re-derives type/kind — re-running a step doesn't re-parse | C# | green |

### Stage 7 — runtime loading

| Behavior | Layer | Sense |
|---|---|---|
| `- load X.dll` registers a `[PlangType]` class via `RegisterRuntime`; resolvable by name | integration cut 4 | green |
| loaded `ITypeRenderer` registered into `TypeSerializers`; value renders via it | integration cut 4 | green |
| overwrite: runtime registration of an existing name wins (`ResolveType` precedence) | C# + integration cut 4 | green |
| loaded `[PlangType]` with no covering renderer → typed load failure | C# | negative |

## 2. Failure matrix

| Failure mode | Detected by | Error type | Layer |
|---|---|---|---|
| Narrowing cast out of range / NaN→int | `number` explicit-OUT cast / `ToInt32` | `OverflowException`/`ArithmeticException` (C# boundary) → `Data.Error` at handler | C# |
| Integer overflow under `Overflow=Throw` | `number.Add` policy path | `Data.Fail("MathOverflow")` (handler), throws internally | C# |
| Divide by zero (integer/decimal) | `number.Divide` | `Data.Fail("DivideByZero")` | C# |
| `Parse`/`Resolve` of a non-numeric string | `number.Resolve` | `Data.Error` (handler), `Parse` returns null | C# + goal |
| `[PlangType]` missing serializer coverage | `PLNG_SerializerCoverage` generator gate | build error (PLNG) | C# generator |
| Runtime-loaded type ships no covering renderer | the loader | typed load failure (mirrors `code.load`) | C# |
| Unknown type name in a `.pr` | `Conversion.TryConvertTo` / registry | `Data.Error` ("Unknown type") | C# |
| `%photo.Path.Exists%` on a base64 image (no source) | navigation | typed null, **not** a crash | C# + goal |

**Impossible-by-design — do NOT write tests for these:** a registered type's serializer lookup missing at runtime (the build gate makes it impossible); a `type:kind` string needing a runtime split (they're separate fields by construction); `number` carrying stale per-request Context (it stores none).

## 3. New surfaces this branch introduces

### Interfaces and types (new)
- `app/data/TypedValueNode.cs` — `sealed record TypedValueNode(object Value, string TypeName)`.
- `app/types/TypeSerializers.cs` — `(typeName, formatToken) → Write` table; `RegisterRuntime(typeName, formatToken, Action<object,IWriter>)`; `Get(typeName, format)`.
- `app/types/ITypeRenderer.cs` — `{ string Format { get; }; void Write(object value, IWriter writer); }`.
- `app/types/number/` — `@this` (sealed class), `NumberKind` enum, `NumberPolicy` struct, `Config : IConfig`, `OverflowMode`/`PrecisionMode` enums.
- `app/types/image/` — `@this` (sealed class).
- `app/types/code/` — `@this` (sealed class).
- `app/types/datetime/` — `@this` (wraps DateTimeOffset). `app/types/duration/` — `@this` (wraps TimeSpan).

### New methods on existing types
- `IWriter.Format { get; }` (+ impls on `json.Writer`, plang writer, `Text`).
- `app/types/Registry.cs` / `app/types/this.cs` — `Get`/`IsPrimitive`/name lookups route through the registry (fold the flat `Primitives` dict).
- Per-type `static string? Build(object? value)` on number/image/code/path.
- `app/data/this.Normalize.cs` — the `TypedValueNode` tag branch.
- `app/channels/serializers/.../Value(object?)` — the `TypedValueNode` case (each writer).
- `file/read.cs` — `Build()` resolves extension → high-level type; `Run()` constructs the typed value.

### New PLang actions
- `math.intdiv` (truncating integer division).
- (`math.*` retyped to `Data<number>` — modified, not new.)

### New registrations
- `[PlangType]` on number, image, code, datetime, duration; `path` retro-registers a `serializer/Default.cs`.
- `image` extension/MIME registrations (`.png`/`.jpg`/`.gif` → image) via `app.formats`.
- `duration` name + `timespan` deprecated alias.
- Per-type `serializer/<format>.cs` files (number Default; image text/protobuf/Default; code Default; path Default).

### Existing surfaces this branch touches by reference (already real)
- `app/types/Registry.cs` — `ResolveName`/`ResolveType`/`RegisterRuntime` (existing; extended, runtime-first precedence relied on).
- `app/config/this.cs` — `For<T>(context)`, the `ConfigScope→parent→Defaults` walk (number policy consumes it).
- `app/modules/IClass.cs` — the action `Build()` hook (file.read uses it; distinct from type `Build`).
- `app/modules/code/load.cs` — the load-scan-register template Stage 7 follows.
- `app/data/this.cs` — `Data.Value` is `object` (why struct boxes); `Data.Type` the routing key.
- `Wire.cs` — value slot already routes through `Normalize` + writer.
- `app/types/Conversion.cs` — `TryConvertTo` (gains a `Resolve(byte[])` branch for binary types).
