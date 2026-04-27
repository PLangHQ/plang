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

## 15. Envelope Pipeline

Data supports a transport pipeline for wrapping, compressing, and encrypting values.

### Outbound: Wrap → Compress → Encrypt

**Wrap:** Creates a category envelope. If the type has a `Kind` (e.g., `"image/jpeg"` → kind `"image"`), wraps in outer Data with `Type = "image"` and inner = original Data. Requires context. PLang primitives (no kind) return self.

**Compress:** If the category type is compressible (e.g., `"text"` is, `"image"` is not), serializes to JSON, GZip-compresses, wraps as `{ type: "archived", value: { type: "gzip", value: byte[] } }`. Returns self if not compressible or no context.

**Encrypt:** Currently a no-op (returns self). Placeholder for future crypto service.

### Inbound: Decrypt → Decompress → Unwrap

**Decrypt:** No-op if type is not `"encrypted"`. Placeholder.

**Decompress:** If type is `"archived"`, reads inner gzip bytes, decompresses, deserializes back to Data. Error cases (all return `Success = false`, `StatusCode = 500`):
- Inner value is not a Data object
- Inner Data has no byte[] value
- Corrupt/invalid gzip data
- Invalid JSON after decompression
- Payload exceeds 100 MB size limit (zip bomb protection)

**Unwrap:** If `Value` is a Data, returns it (strips envelope). Otherwise returns self.

### Properties are NOT preserved through compression
Properties are `[JsonIgnore]` — after compress → decompress, `Properties.Count = 0`. By design: Properties are for the transport view, not intermediate compression.

### Round-trip
`Wrap → Compress → Encrypt → Decrypt → Decompress → Unwrap` preserves the original value.

## 16. Signature

- Defaults to `null`
- Can hold a `SignedData` object with `Type`, `Nonce`, etc.
- Marked with `[Out]` and `[In]` attributes (part of transport view)

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
