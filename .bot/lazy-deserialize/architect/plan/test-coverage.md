# Test Coverage — lazy-deserialize

> **Note for test-designer:** every row below is a **suggestion** of what should be covered, not a contract on test names or organization. You own the suite. Merge rows, split rows, drop redundant ones, add rows for cases you spot — push back on the strategy itself if it looks wrong.

The strategy narrative is in [`test-strategy.md`](test-strategy.md). This file is the heavy reference. One row ≈ one test (or one row of a parameterized test). `Sense`: green = expected-success path, negative = expected-failure path.

## Stage 1 — reader registry + consolidation (no behavior change)

| Behavior | Layer | Sense |
|----------|-------|-------|
| `reader.Of(type, kind)` returns the registered `Read` for an exact `(type, kind)` | C# | green |
| `reader.Of` falls back to the `"*"` (Default.cs) entry when no exact kind match | C# | green |
| `reader.Of` precedence: runtime-exact beats generated-exact beats runtime-`"*"` beats generated-`"*"` | C# | green |
| `reader.Of` returns null when neither exact nor wildcard is registered | C# | negative |
| Discovery indexes a `serializer/Default.cs` static `Read` into `_generated` at startup | C# | green |
| `Register` adds a runtime `Read` that shadows the generated one | C# | green |
| `path.Read` reconstructs a path identically to the old `path.JsonConverter.Read` | C# | green |
| `number.Read` parses identically to the old number convert (pre-Stage-2 behavior) | C# | green |
| Each `FromWire` body, moved to its type's `Read`, produces the same value (crypto.hash) | C# | green |
| Distributed `OwnerOf`: each family declares its CLR types; central `if u==typeof(int)…` switch is gone | C# | green |
| `path.JsonConverter` type is deleted (reflection check it's gone); 6 sites now wire the single json `Converter` | C# | green |
| `type.json` **stays** (reads the type descriptor `{name,kind,strict}`, not a value) — reflection check it's still present | C# | green |
| `ErrorWire` / `HashDataConverter` / `TimeSpanIso8601` deleted as standalone converters; decode logic on each type's `Read` | C# | green |
| Single json `Converter` routes a mid-graph typed field to that type's `Read` (consults the registry / `OwnerOf`) | C# | green |
| **Nested path field — a `path` three levels down in a CLR object (`As<T>`) deserializes via the `Converter`** (the regression `LiftDataIfShaped`-style guessing would miss; credit: coder) | C# | green |
| Residual `TryConvert` plumbing (nullable unwrap, assignable fast-path, list element-walk) still works | C# | green |
| A `Read` failure produces an `Error`, not a throw into a courier | C# | negative |
| Round-trip via renderer→reader for path / image / number / text-json (the keying validation) | C# | green |
| **Full existing suite green with no expected-output edits** (the "no behavior change" pin) | C#+goal | green |
| Snapshot `FromWire` / `app.Snapshot*` signatures unchanged (carve-out) | C# | green |

## Stage 2 — numbers (Way 3)

| Behavior | Layer | Sense |
|----------|-------|-------|
| A `uint` value is stored as `uint`; kind derives as `uint` (not `int`/`double`) | C# | green |
| A `float` value stores as `float`; kind = `float` (not `double`) — the old collapse is gone | C# | green |
| A `BigInteger` stores and round-trips losslessly | C# | green |
| `Kinds` advertises the full tower (sbyte…BigInteger); `KindToClr` maps each | C# | green |
| `number.Read("9999999999999999999999", biginteger)` → `BigInteger`, no loss | C# | green |
| `number` declares the whole tower in the distributed `OwnerOf` | C# | green |
| `int + int` result kind = `int` | C# | green |
| `3000000000u + 2000000000u` → `5000000000` as `long` (no uint wrap) | C# | green |
| integer ⊕ binary float → `double`; integer ⊕ decimal → `decimal` | C# | green |
| `double ⊕ decimal` raises the explicit-cast error | C# | negative |
| division producing a fraction → decimal/double per operands | C# | green |
| Stamp from `value.GetType()` across the tower (`data/this.cs:242` no longer collapses float) | C# | green |
| Goal: a sum that would overflow `uint` lands correctly | goal | green |
| Goal: a `double`+`decimal` step errors | goal | negative |

## Stage 3 — lazy Data

| Behavior | Layer | Sense |
|----------|-------|-------|
| `.Value` materializes via the reader when `_value` is null and `_raw` is set | C# | green |
| Authored value (`_value` set, `_raw` null) returns `_value`; never invokes the reader | C# | green |
| `%var%` in an authored value resolves fresh per read (unchanged contract) | C# | green |
| `_raw` survives materialization (still set after `.Value` is read) | C# | green |
| Untouched raw-backed Data serializes `_raw` verbatim (byte-identical) | C# | green |
| A mutation (`SetValueDirect` / navigation-set) invalidates `_raw`; serialize then renders from `_value` | C# | green |
| `_raw` is a string for text sources, `byte[]` for binary (no utf-8 encode of text) | C# | green |
| `ConvertValue` is removed; navigation reads `.Value` (materializes) | C# | green |
| `_valueFactory` / `DynamicData` recompute-on-every-access still works (distinct laziness) | C# | green |
| `Wire.Read` captures the value slot raw into `_raw` and defers (no eager `Deserialize<object?>`) | C# | green |
| `LiftDataIfShaped` is deleted (reflection/behavior check); no `name`+`value` shape sniff | C# | green |
| Nested Data rebuilt by the containing type's reader (e.g. `Signature`), not a key-shape guess | C# | green |
| Malformed json errors at first touch, not at read; error names the source | C# | negative |

## Stage 4 — one I/O boundary

| Behavior | Layer | Sense |
|----------|-------|-------|
| `channel.read` stamps `type`/`kind` from `Mime` and produces lazy Data | C# | green |
| Stream channel no longer returns bare text | C# | green |
| All channel kinds live under `channel/type/` (stream, session, message, event, goal, noop, file, http) | C# | green |
| `file` channel reads bytes via `path.ReadBytes` (AuthGate enforced); no `System.IO` in the channel (PLNG002 clean) | C# | green |
| `file.read` opens the file channel; no read-time `Type.Convert` in `FilePath.ReadText` | C#+goal | green |
| `http` channel is bidirectional; body→value, status/headers/duration→properties | C# | green |
| `http.get` opens the http channel; stops `Content-Type` deserialize | C# | green |
| `http.response.@this` deleted (reflection check); result is plain Data | C# | green |
| `%response!status%` reads without materializing the body | goal | green |
| `%response.field%` materializes the body | goal | green |
| `TypeFromMime(application/json)` → `{object, json}` (shape-based; keeps today's json→object) | C# | green |
| `TypeFromExtension`: `.json` → `{object, json}`; `.csv` → `{table, csv}`; `.xlsx` → `{table, xlsx}`; `.png` → `{image, png}` | C# | green |
| `config.json` read lands as `{object, json}`; `%cfg%` untouched is the json string (no parse from stamping) | goal | green |
| `report.csv` lands as `{table, csv}`; the `(table, csv)` reader materializes a grid on navigation | C#+goal | green |
| `table` navigates by row/column once touched; `object` navigates by key | C#+goal | green |
| `(table, xlsx)` reader is a follow-on — a `.xlsx` stamps `{table, xlsx}` and rides as raw bytes until then | C# | green |
| `application/plang` body → the Data container (lazy `Wire.Read`); `application/json` body → an `{object, json}` value | C# | green |

## Stage 5 — access-driven resolution

| Behavior | Layer | Sense |
|----------|-------|-------|
| Scalar `%x%` of a bytes-backed value decodes utf-8 | C# | green |
| Scalar `%x%` of non-utf-8 bytes stays bytes (no parse) | C# | green |
| Scalar `%x%` of a text value returns the string (no structured parse) | C# | green |
| Navigation `%x.field%` of a known type materializes via the reader and navigates | C#+goal | green |
| Navigation of a type-unknown value → "value has no type; add `as <type>`" error | C#+goal | negative |
| `%x as json%` / `As<T>` reads a type-unknown value toward that type | C#+goal | green |
| Property `%x!prop%` reads from `Properties`; value never materialized | C# | green |
| No content sniffing — type-unknown structured access never guesses json/xml/yaml/csv | C# | negative |

## Cross-cutting / integration (the named cuts in test-strategy.md)

| Cut | Layer | Sense |
|-----|-------|-------|
| Cut 1 — verbatim passthrough: untouched Data serializes raw byte-identical; reader never invoked | integration | green |
| Cut 2 — touch materializes: config.json object→navigate, csv table→row/col, big-int, image-on-property | integration | green |
| Cut 3 — sign→wire→verify on raw without materializing; nested signed Data round-trips | integration | green |
| Cut 3 — tampered raw fails verification | integration | negative |
| Cut 4 — http body lazy, status/headers eager; http.response gone | integration | green |
| Cut 5 — number tower round-trip preserves exact kind; promote-then-narrow; double⊕decimal errors | integration | green/negative |
