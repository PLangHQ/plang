# coder v3 — addresses codeanalyzer v2 (V1)

**Status:** V1 closed. C# 3381/3381 (+1 fixture); PLang 233/233.

## V1 — `json.Writer.EndRecord` hard-coded `View.Out` for inner-Data Properties

Codeanalyzer correctly identified this as the second M2 site. `Wire.Write` propagated the converter's `View` into the outer Properties walk, but `json.Writer.EndRecord` (the inner-Data emission path) hard-coded `View.Out` when normalizing Property values. A Store-mode outer walk on a Data graph with an inner Data carrying Properties would silently strip `[Sensitive]` and `[Store]`-only content from the inner Properties.

### Fix

Added a `_view` field to `json.Writer`:

```csharp
public Writer(Utf8JsonWriter writer, JsonSerializerOptions? options = null,
    app.View view = app.View.Out)
```

`EndRecord` now passes `_view` into `Data.NormalizeValue` for each Property value. `Wire.Write`'s construction site threads `View` into the writer:

```csharp
var jsonWriter = new app.channels.serializers.json.Writer(writer, options, View);
```

Default ctor stays `View.Out` so existing tests + callers (test helpers, the `NormalizePipelineHelper`) continue to behave as before.

### Test

`CanonicalizationTests.StoreView_PropagatesIntoInnerDataProperties_NotHardcodedToOut`:

- Outer Data with Value = inner Data
- inner.Properties contains a `List<object>` whose element is an `Identity` with `PrivateKey = "PRIV-must-persist"`
- `[Sensitive]` is excluded by `View.Out`, included by `View.Store`
- Out path asserts the secret never reaches the wire
- Store path asserts the secret survives the local persistence write

**Mutation-verified:** reverting the EndRecord call back to hard-coded `View.Out` makes the test fail at the Store-bytes assertion (secret missing from storeBytes), as expected. Reverted; the assertion now passes only when View is correctly threaded.

The fixture also covers the gap codeanalyzer noted: Properties' value-shape gate (`Properties.EnsureSupportedValue`) is shallow — it rejects direct Data/domain objects but doesn't recurse into collections. A `List<object>` containing an Identity passes the gate, which is the realistic path by which view-sensitive content gets into Properties.

## Tests

| Suite | Result | Notes |
|-------|--------|-------|
| C# | 3381/3381 | +1 (V1 fixture) |
| PLang | 233/233 | unchanged |

## Verdict for next bot

V1 closed. View now threads symmetrically from `Wire.Write` → outer Properties → inner-Data inline emission → inner Properties. Back to codeanalyzer for v3.
