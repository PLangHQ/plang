# Data — Behavioral Specification

> Derived from unit tests. Each section states what the tests assert as true behavior.
> If any statement here contradicts your understanding of how Data *should* work, the tests may be wrong.

## 1. Construction

| Constructor call | `Name` | `Value` | `IsInitialized` | `Type` |
|---|---|---|---|---|
| `new Data("x")` | `"x"` | `null` | `false` | `null` |
| `new Data("x", "hello")` | `"x"` | `"hello"` | `true` | lazy → `"string"` |
| `new Data("x", null)` | `"x"` | `null` | `false` | `null` |
| `new Data("x", 42)` | `"x"` | `42` | `true` | lazy → `"int"` |
| `new Data("x", "hello", Type.String)` | `"x"` | `"hello"` | `true` | `Type.String` (explicit, not overridden) |

**Name cleaning:**
- `%varName%` → `"varName"` (percent signs stripped)
- `"  spacedName  "` → `"spacedName"` (trimmed)

**Timestamps:** `Created` and `Updated` are set to `DateTime.UtcNow` at construction.

**Properties:** Always initialized to an empty `Properties` collection (count = 0).

**Parent:** Optional. When not provided, `null`. When provided, the child's `Context` is inherited from the parent.

## 2. IsInitialized

The rule: **`IsInitialized = (value != null)` at construction time.**

- Constructing with `null` → `IsInitialized = false`
- Constructing with any non-null value → `IsInitialized = true`
- Setting `Value` via the setter **always** sets `IsInitialized = true`, even if you set it to the same value

The `Value` setter marks `IsInitialized = true` unconditionally — the act of setting a value means it was initialized, regardless of what the value is.

## 3. Value Setter Side Effects

Setting `Value` does three things beyond storing the value:
1. Sets `IsInitialized = true`
2. Updates `Updated` timestamp
3. **Clears `_type`** (forces lazy re-derivation on next `Type` access)

This means: if you construct with an explicit `Type`, then set `Value`, the explicit type is lost and will be re-derived from the new value.

## 4. Type System

### Lazy derivation
If no explicit `Type` is provided, it's derived on first access from the value:
- `42` → `Type.Value = "int"`, `ClrType = typeof(int)`
- `"hello"` → `Type.Value = "string"`, `ClrType = typeof(string)`
- `null` value → `Type` returns `null`

### Explicit type is preserved
If you pass a `Type` to the constructor, it is used as-is and not overridden by lazy derivation — **until** you set `Value`, which clears it.

### Context propagation
Setting `Context` on Data propagates it to the `Type` object. This enables:
- `Type.Kind` — category of a MIME type (e.g., `"image/jpeg"` → `"image"`). Returns `null` without context.
- `Type.Compressible` — whether the kind benefits from compression. `false` without context.
- `Type.ClrType` — can resolve through context's `App.Types` or fall back to static `TypeMapping`.

### Type invalidation
The `Value` setter clears `_type`, so re-accessing `Type` after changing `Value` gives a freshly derived type:
```
data = new Data("x", "hello")  →  Type.Value = "string"
data.Value = 42                 →  Type.Value = "int"
```

## 5. Path Building

| Parent | Child Name | Child Path |
|---|---|---|
| none | `"testVar"` | `"testVar"` |
| `"parent"` | `"Name"` | `"parent.Name"` |
| `"items"` | `"0"` (numeric) | `"items[0]"` |

Rule: numeric child names use bracket notation, non-numeric use dot notation.

## 6. GetValue\<T\>

- If the stored value is already `T`, returns it directly.
- If convertible (e.g., `int` → `double`), converts via `TypeMapping.ConvertTo`.
- If incompatible (e.g., `"hello"` → `int`), returns `default(T)` (not an exception).

Non-generic `GetValue(Type)`:
- Returns `null` if the stored value is `null`.
- Returns the value directly if assignable.
- Otherwise attempts conversion via `TypeMapping`.

## 7. IsEmpty

`IsEmpty` is true when ANY of:
- `IsInitialized == false`
- `Value == null`
- `Value` is an empty string (`""`)

Note: a non-null, non-string value (e.g., `0`, `false`) is NOT empty.

## 8. Data.Null(name)

`Data.Null("test")` creates a Data with:
- `Name = "test"`
- `Value = null`
- `IsInitialized = false`

This is the "no value found" sentinel used throughout navigation.

## 9. ToBoolean

`ToBoolean()` returns `IsInitialized`. A Data wrapping a non-null value is "truthy"; a Data with null value is "falsy".

## 10. Success / Error

- `Success` = `Error == null` (no error means success)
- `Data.Ok()` → `Success = true`, `Value = null`
- `Data.Ok(value)` → `Success = true`, `Value = value`
- `Data.FromError(error)` → `Success = false`, `Error = error`
- Implicit `bool` conversion uses `Success`, not `IsInitialized`

**`Handled`:** When true, signals that a before-event handled this step. Default: `false`.

**`Returned`:** Signals RunSteps to stop iteration. Default: `false`.

**`ReturnDepth`:** How many goal boundaries a return crosses. Default: `1`.

## 11. ToString

- With a value: returns `Value.ToString()` (e.g., `42` → `"42"`)
- With null value (but Success): returns `"(null)"`
- With error: returns `"Error: {message}"`

## 12. Navigation (GetChild)

### Dot notation
`data.GetChild("user.name")` navigates nested dictionaries:
```
{ "user": { "name": "John" } }  →  GetChild("user.name").Value = "John"
```

### Bracket/index notation
`data.GetChild("[1]")` indexes into lists. Zero-based.

### Mixed notation
`data.GetChild("users[1].name")` combines both.

### Property reflection
Works on CLR objects: `data.GetChild("Name")` reads the `Name` property via reflection.

### Case insensitive
Property and dictionary key lookups are case-insensitive.

### Not found behavior
- Nonexistent path → returns Data with `IsInitialized = false`
- Out of bounds index → returns Data with `IsInitialized = false`
- Negative index → returns Data with `IsInitialized = false`
- Navigation on null value → returns Data with `IsInitialized = false`

Never returns `null`. Always returns a Data object.

### Context inheritance
Child Data created via `GetChild` inherits the parent's `Context`.

### Depth limit
Paths exceeding 100 segments return a Data with:
- `Success = false`
- `Error.Key = "NavigationDepthExceeded"`
- `Error.StatusCode = 400`

### Infrastructure access (! prefix)
`!Name`, `!Error`, `!Success` access Data's own properties rather than navigating the value. Without `!`, `%user.name%` navigates into the value's "name" property; only `Success`, `Error`, and `Name` are available as fallback without the prefix.

### Method calls
Navigation supports chainable method-like syntax:
- `grep("pattern")` — search content
- `grepcount("pattern")` — count matches
- `maxLength(100)` — truncate with "..."
- `trim()` — whitespace trim
- `toLower()` / `toUpper()` — case conversion
- `replace("old", "new")` — string replacement

## 13. Merge

Treats both `Value`s as `List<Data>`, merges by `Name` (case-insensitive):
- Same name → replaced
- New name → appended
- Order: existing items first, new items appended at end

Edge cases:
- `other.Value == null` → returns `this` (no-op)
- Non-list values → both cast as `List<Data>` produce empty lists, result is empty list

## 14. Clone

Deep-clones the value. Preserves: `Name`, `Type`, `Error`, `Handled`, `Returned`, `ReturnDepth`, `Warnings`, `Signature`, `Properties` (shallow clone of collection), `Context`.

## 15. Transport Pipeline

Data supports a transport pipeline for wrapping, compressing, and encrypting values. The methods live on `app/data/this.Transport.cs` (renamed from the old `this.Envelope.cs` on `data-serialize-cleanup`).

### Outbound: Wrap → Compress → Encrypt

**Wrap:** Creates a category wrapper Data. If the type has a `Kind` (e.g., `"image/jpeg"` → kind `"image"`), wraps in outer Data with `Type = "image"` and inner = original Data. Requires context. PLang primitives (no kind) return self.

**Compress:** If the category type is compressible (e.g., `"text"` is, `"image"` is not), routes the Data through the registered `application/plang` serializer to produce bytes, GZip-compresses them, and returns a flat archived Data: `{ name: "", type: "archived", value: byte[] }`. No inner `gzip` Data — the byte[] is the `Value` directly. Returns self if not compressible or no context.

**Encrypt:** Currently a no-op (returns self). Placeholder for future crypto service.

### Inbound: Decrypt → Decompress → Unwrap

**Decrypt:** No-op if type is not `"encrypted"`. Placeholder.

**Decompress:** If type is `"archived"`, reads `Value` as `byte[]`, GZip-decompresses, deserializes via the registered `application/plang` serializer back to Data. Error cases (all return `Success = false`, `StatusCode = 500`):
- `Value` is not a `byte[]`
- Corrupt/invalid gzip data
- Invalid JSON after decompression
- Payload exceeds 100 MB size limit (zip bomb protection)

**Unwrap:** If `Value` is a Data, returns it (unwraps the outer Data). Otherwise returns self.

### Properties round-trip through compress/decompress
Because `Compress` routes through the same `application/plang` serializer that channels use, `Properties` ride along inside the bytes (the wire shape's `properties` field — Stage 4). After `Compress → Decompress`, `Properties` are preserved. The legacy "Properties are `[JsonIgnore]`" constraint no longer applies.

### Signatures and the wire round-trip
Because `Compress` routes through `application/plang`, `Wire.Write` calls `EnsureSigned()` sign-if-missing on every Data it walks. The inner Data's signature rides inside the compressed bytes; the outer `archived` Data gets its own signature when *it* later crosses a channel. Sign-then-compress and compress-then-sign produce equivalent wire shapes — forwarding preserves Alice's attestation.

### Round-trip
`Wrap → Compress → Encrypt → Decrypt → Decompress → Unwrap` preserves the original value (including its `Properties`).

## 15a. Properties — sidecar metadata

`Properties` is the per-Data sidecar bag: `IDictionary<string, object?>` (case-insensitive). On the wire it lives in its own nested `properties` object — a fifth top-level field alongside `name`, `type`, `value`, `signature`. Omitted when empty.

**Insertion gate.** `Properties[key] = value` admits only wire-supported primitives: `string`, `bool`, `int`, `long`, `double`, `decimal`, `DateTime`, `byte[]`, `null`, plus `IDictionary<string,object?>` and `IEnumerable<object?>` shapes built from those. Raw `Data` instances are rejected — Properties are metadata *about* the Data, not nested Datas that need their own attestations. Unsupported shapes throw `ArgumentException`; `variable.set` callers see `InvalidVariableReference` (400).

**Keys are unconstrained.** Because Properties live in their own nested object, key names don't collide with the reserved top-level fields — `Properties["value"]` is fine; it lives at `properties.value`, not at the root.

**Access from PLang.** Two operators, two stores:

| Expression       | Reads                          |
|------------------|--------------------------------|
| `%x.field%`      | `x.Value` navigation (existing) |
| `%x!key%`        | `x.Properties[key]` lookup (Stage 4) |
| `%x!key.path%`   | `x.Properties[key]`, then dot-navigate within the dict/list |

`%x!key%` is the **mid-expression `!`** — between an identifier and a key. The leading-`!` shape `%!name%` (infrastructure namespace: `%!data%`, `%!error%`) is positionally distinct and still parses as a single variable reference. Malformed shapes (`%x!!cost%`, `%x.y!cost%`, `%!x!cost%`) flag `Variable.IsMalformed`; `variable.set` rejects them up front.

**Writing Properties.** `set %x!key% = value` writes through `variable.set` to `Properties[key]`. Type round-trips faithfully through the wire (numbers may promote `int → long` via JSON; explicit `string`/`bool`/`DateTime` survive intact).

## 16. Signature

- Defaults to `null`
- Holds a `SignedData` record (`signing.SignedData`) with `Type`, `Nonce`, `Algorithm`, `Headers`, etc.
- Marked with `[Out]` and `[In]` attributes (part of transport view)
- `Wire.Write` calls `EnsureSigned()` sign-if-missing on egress — every Data the converter walks auto-seals if its `Signature` is null. Idempotent: already-signed Data is skipped. Forwarded payloads preserve nested provenance (Alice's inner signature rides intact under Bob's outer signature). (The class was named `WireJsonConverter` until `data-normalize` renamed it to `Wire`.)
- Canonicalization for `crypto.Hash` uses the same options bag (`plang.@this.OutboundOptions`) the wire writer uses. Hash bytes ≡ wire bytes minus the outer `Signature` field. Tampering with `name`, `type`, `value`, `properties`, or any nested-Data signature invalidates the outer signature.

## 16a. Wire shape: `Normalize` → `IWriter` → bytes

Landed on `data-normalize` (followed `data-serialize-cleanup`). Replaces the
old "value slot is whatever JsonSerializer thinks of the CLR value" pattern
with a uniform two-stage pipeline: `Normalize` walks any C# value into a
tree of `primitive | byte[] | Data | List<>`; an `IWriter` impl walks that
tree and emits format-specific bytes. JSON is the first writer; protobuf
or CBOR ship later as siblings without touching `Normalize` or any domain
type.

### `Normalize` — the tree walker

`PLang/app/data/this.Normalize.cs`. `data.Normalize(View)` returns a uniform
tree whose every node is one of:

- a primitive (`bool`, integral, floating, `decimal`, `string`, `DateTime`,
  `DateTimeOffset`, `TimeSpan`, `Guid`, enum),
- `byte[]`,
- a nested `Data` (`app.data.@this`), or
- a `List<object?>` of the above.

Domain objects (`Identity`, `path.@this`, `setting`, `Variable`, …) decompose
into nested `Data` nodes whose `Name` is the lowercased property name and
whose `Value` is the property's normalized value. **Which properties ship is
decided by the `View` argument** (see [`Tagged`](#tagged-filter) below).

Bounds:

- **128-depth cap** (`MaxNormalizeDepth`) — mirrors the rehydration cap.
  Exceeded → `NormalizeException` with key `NormalizeMaxDepthExceeded`.
- **Visited-set cycle detection.** Direct or indirect cycles in object graphs
  → `NormalizeException` with key `NormalizeCycleDetected`. The set is
  scoped to a single Normalize call; sharing an object across siblings is
  fine.
- **Getter exceptions** are wrapped as `NormalizeGetterThrew` with the
  original exception attached — never silently swallowed.

The walker is *observation-only*: it builds a fresh tree and never mutates
the source object.

### `IWriter` and `json.Writer`

`PLang/app/channels/serializers/IWriter.cs`. Format-encoder protocol with
three concerns: leaf primitives (`Null`/`Bool`/`Int`/`Long`/`Float`/`Double`/
`String`/`DateTime`/`DateTimeOffset`/`TimeSpan`/`Guid`/`Enum`/`Bytes`/
`Decimal`), array brackets (`BeginArray(int count)`/`EndArray` — count is
advisory; JSON ignores it, length-prefixed binary formats may use it), and
record brackets (`BeginRecord`/`EndRecord(@this record)` — the record-end
overload takes the source Data so the writer can lay out the canonical
envelope per-format).

`PLang/app/channels/serializers/json/writer.cs` is the first impl. Wraps
`Utf8JsonWriter`; `EndRecord` emits the five-field envelope. The writer
never reflects — `Normalize` has already decomposed any C# object into a
tree, so writing is pure dispatch on the runtime type of each visited node.

### `Tagged` filter

`PLang/app/channels/serializers/filters/Tagged.cs`. `(type, View)`-cached
property selector. Decides which CLR properties ship per view:

| View          | Ships                                                                                          |
|---------------|------------------------------------------------------------------------------------------------|
| `View.Out`    | `[Out]`-tagged only. `[Sensitive]` excluded. `[Masked]` ships with value `"****"`.            |
| `View.Store`  | `[Store]`-tagged only. **`[Sensitive]` and `[Masked]` are ignored** — local persistence path. |
| `View.Debug`  | Every public instance property. `[Sensitive]` still excluded. `[Masked]` still emits `"****"`.|

Reflection fires once per `(type, View)` per process; results are concurrent-
dictionary cached. Filter results carry a `Masked` flag so `Normalize` can
emit `"****"` without invoking the getter.

**Transparent fallback.** When a CLR type has no view-relevant tags at all,
`Tagged` treats every public property as in-view ("untagged"). Architect-
intentional: `verb.@this` and similar interior types flow through `Normalize`
without needing explicit tagging.

### `View.Store` — why it exists

The local persistence path (`Sqlite.Set` → `plang.Store(data)`) cannot use
`View.Out`: `Identity.PrivateKey` is `[Sensitive, Store]` — it must
round-trip the local DB but never cross a channel. `View` is now per-
instance on `Wire` (not AsyncLocal); the plang serializer keeps separate
`JsonSerializerOptions` for outbound vs store, each carrying its own `Wire`
converter. The storage-vs-wire decision is visible at the construction site.

**Signing is always Out-canonical.** Even when persisting via Store, the
sign step re-canonicalises through `OutboundOptions` (`View.Out`) under
`MarkOuterForHash`. So the signature stored alongside the Store-view bytes
matches the Out-view bytes any later `signing.verify` will see — no
view-drift.

### `[Masked]` attribute

`PLang/app/View.cs`. Property-level attribute. Marks a property whose name
ships visibly but whose value ships as the literal string `"****"`. Distinct
from `[Sensitive]` (which excludes the property entirely). Honored in
`View.Out` and `View.Debug` views; ignored under `View.Store`.

Canonical case: `setting.value` is `[Out, Masked]`. Receivers see
`{key: "DATABASE_URL", value: "****"}` — they know the setting is
configured without seeing the secret.

### `Reconstruct<T>` — the reverse walker

`PLang/app/data/this.Reconstruct.cs`. `data.Reconstruct<T>(Context)` walks
a normalized tree back into a CLR `T`. Dispatch order:

1. **Primitive / enum / `string` / `byte[]` / `decimal` / `DateTime`** —
   `AppTypes.ConvertTo`.
2. **`List<X>`** — walks each child element through `Reconstruct<X>`.
3. **`Dictionary<K,V>`** — each child Data's `Name` becomes the key,
   `Value` walks into `V`.
4. **Per-type hook (wins over property-bag).**
   - Explicit `public static T FromNormalized(Data, Context)` on `T`.
   - Built-in `path.@this` hook: reads the `"relative"` child and calls
     `path.Resolve(relative, ctx)`, yielding the scheme-correct subclass.
     Without a `Context`, raises `NormalizeContextRequired`.
5. **Generic property-bag** — parameterless ctor + lowercased-name property
   setter dispatch. Positional-ctor types fall back to a longest-public-ctor
   walk (single-primary-ctor records are the common case).

Typed errors via `NormalizeException.Key`:

| Key                                  | Meaning                                                            |
|--------------------------------------|--------------------------------------------------------------------|
| `NormalizeNoReconstructionStrategy`  | Target has no parameterless ctor and no `FromNormalized` hook.    |
| `NormalizeContextRequired`           | `path`-assignable target reconstructed without a `Context`.       |
| `NormalizeMissingRelative`           | Path tree lacked the `"relative"` child.                          |
| `NormalizeReconstructFailed`         | Setter or ctor threw mid-walk (wraps original).                   |
| `NormalizeUnexpectedLeafType`        | Leaf node didn't match the expected primitive type.               |
| `NormalizeGetterThrew`               | (Normalize side, surfaced symmetrically.)                         |
| `NormalizeCycleDetected`             | (Normalize side, symmetric.)                                      |
| `NormalizeMaxDepthExceeded`          | (Normalize side, symmetric.)                                      |

No silent swallowing, no `catch { }`. Getter / ctor exceptions are always
re-thrown with the original exception attached.

### Known follow-ups (not blocking on this branch)

- **Positional-ctor required-param silent default.** When a positional-record
  ctor has a required reference param and the normalized tree lacks the
  matching child, the slot is filled with `null` (value types get
  `default(T)`). The ctor either accepts the defaulted args or throws a
  generic `TargetInvocationException` that surfaces as
  `NormalizeReconstructFailed`. No inventoried domain type
  (`Identity`, `setting`, `Variable`, `path.@this`, `http.Response`) hits
  this today. Fix shape when a use case arrives: synthesize
  `NormalizeMissingRequiredField` ahead of the ctor call.
- **Longest-ctor pick comment.** The comment in `this.Reconstruct.cs:151`
  says "prefer the ctor whose parameter names match available children"
  but the code unconditionally picks the longest public ctor. Today every
  record has one primary ctor so longest-wins lands correctly; the comment
  is aspirational, the behaviour is fine.

### Where to look in code

- `PLang/app/data/Wire.cs` — five-field envelope, sign-if-missing, View-per-instance.
- `PLang/app/data/this.Normalize.cs` — tree builder, depth/cycle bounds.
- `PLang/app/data/this.Reconstruct.cs` — reverse walker + hooks.
- `PLang/app/data/NormalizeException.cs` — typed error keys.
- `PLang/app/channels/serializers/IWriter.cs` — format-encoder protocol.
- `PLang/app/channels/serializers/json/writer.cs` — JSON impl.
- `PLang/app/channels/serializers/filters/Tagged.cs` — `(type, View)` filter.
- `PLang/app/View.cs` — `[Out]`, `[Store]`, `[Sensitive]`, `[Masked]` attributes + the `View` enum.

## 17. Data\<T\> (Generic)

Inherits from `Data`. The `new T? Value` property shadows the base:
- If base value is `T`, returns it directly
- Otherwise attempts `GetValue<T>()` conversion
- If wrong type and no conversion possible, returns `default(T)`

Factory methods:
- `Data<T>.Ok(value)` → typed success
- `Data<T>.FromError(error)` → typed error

Assignable to `Data` — works with `Task<Data>` in the interface chain.

## 18. DynamicData

Computes its value on every access via a factory function. Each `Value` access calls the factory again — not cached.

```
var counter = 0;
var d = new DynamicData("counter", () => ++counter);
d.Value  // 1
d.Value  // 2
d.Value  // 3
```

Can have an explicit `Type` set at construction.

## 19. JSON Unwrapping

Values passed to Data are automatically unwrapped from `JsonElement`:
- `JsonValueKind.String` → `string`
- `JsonValueKind.Number` → `long` (if integer fits), else `decimal`, else `double`
- `JsonValueKind.True/False` → `bool`
- `JsonValueKind.Null/Undefined` → `null`
- `JsonValueKind.Object` → `Dictionary<string, object?>` (case-insensitive keys)
- `JsonValueKind.Array` → `List<object?>`

**Important:** JSON integers become `long`, not `int`. JSON decimals become `decimal`. This matters for generic casts — `(int)(object)longValue` will throw.

Newtonsoft `JToken` values are also unwrapped (v1 compatibility shim) — detected by namespace, no Newtonsoft import required.

Nesting depth limit: 128 levels. Exceeding throws `InvalidOperationException`.
