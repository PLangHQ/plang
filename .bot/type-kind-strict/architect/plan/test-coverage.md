# Test coverage

The heavy reference behind [test-strategy.md](test-strategy.md). Test-designer reads the coverage matrix top-to-bottom and writes one test per row; the failure matrix and surface inventory keep the negative paths and naming honest. Coder also reads this while implementing per-stage tests. Paths are post singular-namespaces merge.

C# test placement follows the repo convention: mirror `app/` under `PLang.Tests/App/` with a `*Tests` PascalCase folder (e.g. `PLang.Tests/App/DataTests/`, `PLang.Tests/App/TypeTests/`) — and **never** create a `PLang.Tests.App.Data`/`Variable` namespace (it shadows the aliases, CS0118). PLang `.goal` tests live under `Tests/` (uppercase).

## 1. Coverage matrix

### Topic: type value model ([type-value-model.md](type-value-model.md))

| Behavior | Layer | Sense |
|----------|-------|-------|
| `type("string")` canonicalises Name to `text` | C# | green |
| `type(name, kind, strict)` carries all three fields | C# | green |
| Single-string `"text/markdown"` splits to `{name:text, kind:markdown}` (then canonicalised) | C# | green |
| Single-string with no slash → `{name, kind:null}`; multi-slash splits on first | C# | green |
| `type` exposes no public `ClrType` (PLang surface is runtime-independent) | C# | green |
| `Data` has no stored `Kind` field; `Data.Kind` reads `Type.Kind` | C# | green |
| Family-`Kind` accessor (`App.Format.KindOf`) removed from the entity | C# | green |
| `type.Kinds` (advertised vocabulary) still populated for `number` | C# | green |
| `Strict` defaults to false | C# | green |
| `Compressible` derives from Name's family (not the old family-`Kind`) | C# | green |
| Wire writes two flat keys `type` + `kind` (no `type:kind`, no `"type":"null"`) | C# | green |
| `type` round-trips through the wire (Name/Kind/Strict intact) | C# | green |
| `Type.Kind` null → `kind` key omitted on the wire | C# | green |
| `App.Type.Kinds` dispatcher renamed (`KindHooks`) — no `Kind`/`Kinds`/dispatcher collision | C# | green |
| `ClrType` reroute: `file/read`, `variable/set`, `settings/Sqlite` still resolve their CLR type | C# | green |

### Topic: kind derivation + validation ([kind-derivation-and-validation.md](kind-derivation-and-validation.md))

| Behavior | Layer | Sense |
|----------|-------|-------|
| `text.Build("report.md")` → `"md"` | C# | green |
| `text.Build("notes")` (no extension) → null | C# | green |
| `text.Build("%var%")` → null | C# | green |
| `text.Build("page.HTML?v=1")` → `"html"` (lowercase, strip query) | C# | green |
| `number.Build(5)`→`"int"`; `Build("3.14")`→`"decimal"`; `Build("1e5")`→`"double"` | C# | green |
| `set %x% = 5` → variable type `{number, int}` | goal | green |
| Runtime mint of a CLR `int` infers `{name:number, kind:int}` (agrees with build) | C# | green |
| Action return declared `int` (e.g. `list.count`) renders `number(int)` in the catalog | C# | green |
| Canonicalise `"markdown"` → `"md"` | C# | green |
| Canonicalise `"jpeg"` → `"jpg"` | C# | green |
| Canonicalise unknown `"frobnicate"` → unchanged (free string) | C# | green |
| Canonicalise a subtype shared by two extensions → primary (`jpg`) | C# | green |
| `App.Format.FamilyOf("image/jpeg")` → `"image"` (renamed from `KindOf`) | C# | green |
| Build stamps `text` kind from a literal `.md` value (via `App.Type.Kinds`/`NormalizeParameterTypes`) | integration | green |
| `image.ValidateKind(gifBytes, "gif")` → `(true, "gif")` | C# | green |
| `image.ValidateKind(pngBytes, "gif")` → `(false, "png")` | C# | negative |
| `set %x% = "readme.md" as text` → variable type `{text, md}` | goal | green |
| `set %x% = "a" as text/markdown` → `kind` normalised to `md` | goal | green |
| `set %img% = "real.gif" as image/gif strict` → builds clean | goal | green |
| `set %img% = "photo.png" as image/gif strict` → build error | goal | negative |
| `set %img% = %upload% as image/gif strict` (mismatch) → runtime typed error | goal | negative |
| `set %x% = "a" as text/markdown strict` (unverifiable) → no byte check, builds clean | goal | green |
| `set %x% = "a" as text` (default, no strict) → kind stamped, no validation | goal | green |

### Topic: LLM type representation ([llm-type-representation.md](llm-type-representation.md))

| Behavior | Layer | Sense |
|----------|-------|-------|
| `TypeSchemas` renders advertised kinds: `number — kinds: int \| long \| decimal \| double` | C# | green |
| `TypeSchemas` renders extension-derived: `text`/`image — kind = extension (…)` | C# | green |
| `BuilderNames` includes `text` | C# | green |
| `BuilderNames` excludes `string`, `int`, `long`, `decimal`, `double` | C# | green |
| System-prompt valid-type list is generated from the catalog (not hand-written) | C# | green |
| Cached system prompt carries the type vocabulary block | integration | green |
| Flat `Primitive types:` line removed from per-step user message | integration | green |
| Per-step catalog block carries only domain/record types | integration | green |
| `type` entry renders as constructor `type(name, kind?, strict?)` for `variable.set` | integration | green |

## 2. Failure matrix

Each row is a way the system *should* fail — the test asserts the failure is hard, typed, and at the right layer.

| Failure mode | Detected by | Error type | Layer |
|--------------|-------------|------------|-------|
| Strict kind mismatch, literal sniffable value | `variable.set.ValidateBuild` → `image.ValidateKind` | build error (BuildValidation) | build / goal |
| Strict kind mismatch, `%var%` sniffable value | `variable.set.Run` → `image.ValidateKind` | typed runtime `ServiceError` | runtime / goal |
| Unknown type name | `variable.set.Run` (`App.Type.Get` → null) | `UnknownType` ServiceError 400 (existing — preserve) | runtime / goal |
| Value not convertible to declared type | `variable.set.ValidateBuild` (existing path) | build error (existing — preserve) | build / goal |
| Malformed multi-slash type string `"a/b/c"` | `type` factory | **not an error** — split on first slash, rest is the (free-string) kind; canonicalises or passes through | C# |
| Unstamped `type.@this` reads a catalog prop without `Context` | `Promote()` | `InvalidOperationException` (existing producer-bug guard — preserve; adding Name/Kind/Strict must not trip it) | C# |

Impossible-by-design — **do not** write tests asserting these fail:

- Strict byte-check on `text` (or any family without an `IKindValidatable` probe). There is no way to verify "plain vs markdown" from content, so strict for `text` degrades to "kind name accepted" and never raises a content-mismatch error. A test that expects `text` strict to catch a wrong format is testing a guarantee the design doesn't make.

## 3. New surfaces this branch introduces

### Interfaces and types

- **`IKindValidatable`** (new marker) — `(bool ok, string? actualKind) ValidateKind(object value, string requiredKind)`. Sibling to `IBooleanResolvable`; `PLang/app/data/IKindValidatable.cs`.
- **`app.type.text.@this`** (new type) — `PLang/app/type/text/this.cs` + `this.Build.cs`. `static string? Build(object?)` (extension→kind), `static string Shape => "string"`, **no** static `Kinds`. Mirrors `image`, text-backed.
- **`app.type.@this`** (restructured entity, `PLang/app/type/this.cs`) — `string Name`, `string? Kind`, `bool Strict`; normalising factory `type(name, kind?, strict?)` + tolerant single-string. `Value` renamed to `Name`; the family-`Kind` accessor and public `ClrType` are gone. Folded catalog props (`Fields`/`Values`/`Kinds`/`Shape`/…) unchanged.

### New methods on existing types

- **`app.type.image.@this.ValidateKind(...)`** — implements `IKindValidatable` via byte-sniff (reuse the ImageSharp `Identify` path). `PLang/app/type/image/`.
- **`app.format.list.@this`** (`PLang/app/format/list/this.cs`) — `KindOf` → `FamilyOf`/`NameOf`; `_extensionToKind` → extension→family map; new kind-canonicalisation helper (subtype→extension, derived from the existing extension↔MIME data).
- **`app.data.@this.Kind`** — delegating accessor onto `Type?.Kind` (or removed); **must not** be a stored field. `PLang/app/data/this.cs`.
- **`app.builder.type.@this.TypeSchemas`** (`PLang/app/builder/type/this.cs`) — second render mode (advertised vs extension-derived), reading off `type.@this`.
- **`app.type.primitive.@this`** (`PLang/app/type/primitive/this.cs`) — `Canonical[typeof(string)] = "text"` and `typeof(int)/long/decimal/double/float` → `number`; `BuilderNames` picks `text`, excludes `int/long/decimal/double`.
- **`app.type.kind.@this`** (`PLang/app/type/kind/this.cs`) — the build-hook dispatcher, reached at `App.Type` renamed `Kinds`→`KindHooks`.

### New PLang actions

- None. `variable.set.Type` changes type (`data.@this<string>?` → a `type`); the action surface is modified, not added.

### New registrations

- None (no new MIME types). The `KindOf`→`FamilyOf` and `Kinds`→`KindHooks` renames are internal.

### Existing surfaces this branch touches by reference

- `variable.set` — `PLang/app/module/variable/set.cs` (`Type` param, `Run`, `ValidateBuild`).
- `NormalizeParameterTypes` — `PLang/app/module/builder/code/Default.cs` (~895; text kind stamping at build).
- `Wire.Write` — `PLang/app/data/Wire.cs` (reads `Type.Name`/`Type.Kind`).
- `Compile.llm` / `CompileUser.llm` — `os/system/builder/llm/` (prompt restructure).
- `ClrType` reroute sites — `PLang/app/module/file/read.cs`, `PLang/app/module/variable/set.cs`, `PLang/app/module/settings/Sqlite.cs`.
