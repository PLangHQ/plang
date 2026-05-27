# Stage 4: Properties get a wire scope

**Goal:** Change `Properties` from `IList<Data>` to `Dictionary<string, object?>` (primitives only), emit Properties as a single nested `properties` object on the wire (a fifth top-level field next to `name`/`type`/`value`/`signature`), and introduce the `%x!key%` access syntax for Properties (vs. `%x.field%` for Value).

**Scope:**
- `PLang/app/data/Properties.cs` — type change: `IList<Data>` → `Dictionary<string, object?>` (or a typed wrapper). Indexed/named access by string key. No reserved-key enforcement: Properties keys are unconstrained because they live in their own object on the wire.
- `PLang/app/data/this.cs:187` — `public Properties Properties { get; set; }` keeps the property name but its backing type changes.
- `PLang/app/data/this.Envelope.cs:43-50` — `Signature`'s `[JsonIgnore]` discipline stays; the new wire converter handles Properties emission via the same `Transport.ForOutbound` re-include pattern as Signature.
- The wire Data converter (the one Stage 2 introduces in `app/data/`) — Write emits Properties as a single nested `properties` object; Read parses `properties` into the Properties dictionary; unknown top-level fields are silently ignored (default STJ behaviour).
- `PLang/app/variables/` — the variable-expression parser learns `!` as the Properties dereference operator. Today `%x.y%` parses as Value-navigation; new `%x!y%` parses as Properties-navigation.

**Out of scope:**
- Public/private split on Properties (debug-only entries that shouldn't cross the wire) — follow-up branch.
- `[Sensitive]` on individual Property keys — follow-up branch.
- Structured (Data-typed) Property values — follow-up branch. Properties is primitives only for this stage.
- Vocabulary sweep (Stage 5).

**Deliverables:**

The new `Properties` C# type:

```csharp
// PLang/app/data/Properties.cs
public sealed class Properties : IDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.OrdinalIgnoreCase);

    public object? this[string key]
    {
        get => _items.TryGetValue(key, out var v) ? v : null;
        set
        {
            EnsureSupportedValue(value);
            if (value == null) _items.Remove(key);
            else _items[key] = value;
        }
    }

    public void Add(string key, object? value) {
        EnsureSupportedValue(value);
        _items.Add(key, value);
    }

    private static void EnsureSupportedValue(object? value) {
        if (value is null or string or bool or int or long or double or decimal
                  or DateTime or byte[]) return;
        if (value is IDictionary<string, object?> or IEnumerable<object?>) return;
        throw new ArgumentException(
            $"Property value of type {value.GetType()} is not a wire-supported primitive.",
            nameof(value));
    }

    // ... IDictionary<string, object?> members forward to _items
}
```

Property keys are unconstrained — any string works. They live inside the `properties` object on the wire, so no collision is possible with the reserved top-level fields. A Property named `"value"` is fine; it lives at `properties.value`, not at the root.

The wire converter (Write side) emits Properties as a nested object:

```csharp
public override void Write(Utf8JsonWriter writer, Data data, JsonSerializerOptions options) {
    if (data.Signature == null) data.EnsureSigned();
    writer.WriteStartObject();
    writer.WriteString("name", data.Name);
    writer.WritePropertyName("type"); JsonSerializer.Serialize(writer, data.Type, options);
    writer.WritePropertyName("value"); JsonSerializer.Serialize(writer, data.Value, options);
    if (data.Properties.Count > 0) {
        writer.WritePropertyName("properties");
        JsonSerializer.Serialize(writer, data.Properties, options);
    }
    writer.WritePropertyName("signature"); JsonSerializer.Serialize(writer, data.Signature, options);
    writer.WriteEndObject();
}
```

(Emit `properties` only when non-empty to keep the wire shape minimal; matches the existing pattern for Signature being omitted when null.)

The Read side parses the five reserved fields:

```csharp
public override Data Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    var data = new Data();
    if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
    while (reader.Read()) {
        if (reader.TokenType == JsonTokenType.EndObject) return data;
        if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
        var key = reader.GetString()!;
        reader.Read();
        switch (key.ToLowerInvariant()) {
            case "name":       data.Name = reader.GetString() ?? ""; break;
            case "type":       data.Type = JsonSerializer.Deserialize<app.data.type>(ref reader, options); break;
            case "value":      data.SetValueDirect(ParseValue(ref reader, options)); break;
            case "properties": data.Properties = JsonSerializer.Deserialize<Properties>(ref reader, options) ?? new(); break;
            case "signature":  data.Signature = JsonSerializer.Deserialize<Signature>(ref reader, options); break;
            default:           reader.Skip(); break;   // unknown top-level field — ignore
        }
    }
    throw new JsonException("Unterminated Data object");
}
```

Sign-if-missing walk skips Properties: the converter calls `EnsureSigned()` on the Data being written; it does NOT recurse into Properties values. (Properties values are primitives or simple JSON shapes, not Data — there's nothing to recurse into. The rule lands by construction.)

Variable expression parser learns `!`:

```
%x.field%        → x.Value navigation         (existing)
%x!key%          → x.Properties[key] navigation                       (new)
%x.field.deep%   → x.Value.field.deep                                 (existing recursion)
%x!key.path%     → x.Properties[key] as primitive/dict, then dot-navigate within (new)
```

The `!` is a single token at the boundary between a variable name and the dereference. Subsequent `.` continues navigation into the dereferenced value (so `%response!cost.input%` reads `Properties["cost"]["input"]` if cost is a dict).

**Dependencies:** Stage 2 (the wire converter is where Properties emission lives; the registered serializer is where Properties round-trip).

## Design

**Why Properties should not be List<Data>.** Today's `Properties : IList<Data>` would, after the sign-if-missing rule, drag every Property entry into the converter's sign walk. An LLM response with cost + tokens + latency would carry four signatures (outer + three Property-Datas). The wire shape becomes obviously wrong: Properties are metadata *about* the Data, not domain content — they shouldn't grow their own attestations. The Dictionary-of-primitives shape removes the entire question: there's no Data inside Properties for the walker to find.

**Why nested `properties: {…}` instead of flat top-level fields.** Earlier sketches considered emitting each Property entry as a top-level sibling of `name`/`type`/`value`/`signature`. The flat shape had ergonomic appeal for the LLM-response case (`cost` reads as a sibling of `value`) but lost on three other axes:

1. *Forward-compatibility for new reserved fields.* PLang is going to evolve. If we later add a top-level `traceId`, the flat shape would conflict with any Property keyed `traceId` — receivers would need to handle the ambiguity, or insertion-time guards would have to grow to cover the new key. With nested, Properties live in their own scope; PLang can add reserved fields freely without touching Property semantics.
2. *Self-documenting wire shape.* A reader scanning the JSON sees five fields with clear roles: `name`, `type`, `value`, `properties`, `signature`. With flat, the reader has to know which top-level keys are reserved and which are extra metadata — schema knowledge required.
3. *Future per-Property metadata.* Public/private split, `[Sensitive]` stripping, structured Property values — all are easier to extend with a nested scope (`properties: { cost: { value: 100, visibility: "public" } }` becomes a natural future shape). The flat form forces side-band schemes for any per-Property annotation.

The LLM-response ergonomic case isn't strong enough to outweigh those three. Wire-shape clarity is more important than a small aesthetic gain in one workflow.

**Property keys are unconstrained.** A consequence of nested: Properties can have *any* key. `"value"`, `"name"`, `"signature"` — all fine, because they live one level deep at `properties.{key}`, not at the root. This is simpler than the flat shape's reserved-key rule and removes a class of validation surface.

**Why two operators (`.` vs `!`) and not collision detection.** The Value-namespace and Properties-namespace are *different stores* on the same Data. A typed `Data<User>` where User has `Kind` and the Data also has `Properties["kind"] = "admin"` — both exist; both are addressable; they mean different things. Operator-namespacing (dot vs bang) makes the call site explicit. Collision detection would force one to mask the other (which?) and break the symmetry. Two operators, two stores, no ambiguity.

**The `[Out]` discipline question deferred.** Earlier conversation raised whether Properties should be opt-in (`[Out]` on each entry) or opt-out (`[OutIgnore]`). Settled: all Properties cross the wire for this branch. Per-entry filtering (public/private, sensitive) is real future work but doesn't block this stage.

**Round-trip invariant.** A Data with `Properties["cost"] = 100`, written through `application/plang`, read back: `data.Properties["cost"]` returns `100` (boxed `int`). The deserializer doesn't lose type information because primitives' JSON encoding is faithful (`100` → JsonElement.GetInt64() → store as `long` or `int`). Document the type-promotion behaviour (int → long after JSON round-trip) so coder can write the test.

**Unknown top-level fields are ignored.** When a wire JSON contains a top-level field outside the five reserved ones (e.g. a future PLang adds `traceId` and an older receiver gets the wire), the converter silently skips it. This is the default STJ behaviour and matches the spirit of "be liberal in what you accept." If we need lossless round-trip for unknown fields later, we'll add an explicit capture bag — for now the simpler ignore-and-skip rule is right.

**Risks:**
- Callers reading `properties.PropertyName` (using the existing `IList<Data>` C# API surface — accessing list items by index) will break. The `IDictionary<string, object?>` surface is different. Compile errors guide the migration. Audit `Properties[...]` call sites.
- The `[JsonIgnore]` on Properties on `data/this.cs:187` may need to flip to `[JsonIgnore]` + `[In]` + `[Out]` to participate in Transport filter (same as Signature does). Coder confirms.
- Variable parser change: `%x!y%` is new syntax. Any existing variable expression containing `!` as a regular character (e.g., the negation prefix `%!flag%`) needs disambiguation. Today `%!flag%` is the boolean-negation operator on a variable — that's at the start of the expression, before any identifier. The new `!` is between identifier and key (`%x!y%`), so positionally distinct. Lex carefully.
- A Property with a structured value (e.g., `Properties["details"] = new Dictionary<string,object?>(...)`) is *allowed* (the `EnsureSupportedValue` admits dict + list of primitives), but Properties round-trip preserves it as the underlying JSON shape, not as a Data. Document that Properties values are *not* Data-instances on either side.

**What the coder verifies:**
- `Properties[key] = value` for each supported primitive type round-trips through `application/plang`.
- `Properties[unsupported-type]` throws with a clear message.
- Wire JSON of a Data with `Properties["cost"] = 100`: nested `"properties": { "cost": 100 }`, not a top-level `"cost"`.
- Wire JSON of a Data with empty Properties: the `properties` field is omitted (matches the Signature-when-null behaviour).
- A Data with `Properties["value"] = "x"` round-trips intact (Properties keys are unconstrained, no reserved-key check).
- Variable parser: `%response.text%` and `%response!cost%` resolve to different stores when both are populated.
- Receive a JSON with unknown top-level field (`traceId`): the field is silently ignored, no error, Properties unaffected.
- Signing: a Data with Properties has exactly one signature (the outer); tampering with the `properties` object in the wire JSON invalidates the outer signature (proves canonicalization covers Properties).
