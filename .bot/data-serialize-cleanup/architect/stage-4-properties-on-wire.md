# Stage 4: Properties flatten to the wire

**Goal:** Change `Properties` from `IList<Data>` to `Dictionary<string, object?>` (primitives only), emit every Property entry as a top-level wire field next to `name`/`type`/`value`/`signature`, and introduce the `%x!key%` access syntax for Properties (vs. `%x.field%` for Value).

**Scope:**
- `PLang/app/data/Properties.cs` — type change: `IList<Data>` → `Dictionary<string, object?>` (or a typed wrapper). Indexed/named access by string key.
- `PLang/app/data/this.cs:187` — `public Properties Properties { get; set; }` keeps the property name but its backing type changes.
- `PLang/app/data/this.Envelope.cs:43-50` — `Signature`'s `[JsonIgnore]` discipline stays; the new wire converter handles flattening.
- The wire Data converter (the one Stage 2 introduces in `app/data/`) — Write emits Properties as flat top-level fields; Read consumes everything-not-reserved into Properties.
- `PLang/app/variables/` — the variable-expression parser learns `!` as the Properties dereference operator. Today `%x.y%` parses as Value-navigation; new `%x!y%` parses as Properties-navigation.
- Reserved-key enforcement: an analyzer (or a runtime guard on `Properties` insertion) rejects keys `name`, `type`, `value`, `signature`.

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
    private static readonly HashSet<string> Reserved =
        new(StringComparer.OrdinalIgnoreCase) { "name", "type", "value", "signature" };

    private readonly Dictionary<string, object?> _items = new(StringComparer.OrdinalIgnoreCase);

    public object? this[string key]
    {
        get => _items.TryGetValue(key, out var v) ? v : null;
        set
        {
            EnsureNotReserved(key);
            EnsureSupportedValue(value);
            if (value == null) _items.Remove(key);
            else _items[key] = value;
        }
    }

    public void Add(string key, object? value) {
        EnsureNotReserved(key);
        EnsureSupportedValue(value);
        _items.Add(key, value);
    }

    private static void EnsureNotReserved(string key) {
        if (Reserved.Contains(key))
            throw new ArgumentException(
                $"Property key '{key}' is reserved on the wire shape.", nameof(key));
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

The wire converter (Write side) emits Properties flat:

```csharp
public override void Write(Utf8JsonWriter writer, Data data, JsonSerializerOptions options) {
    if (data.Signature == null) data.EnsureSigned();
    writer.WriteStartObject();
    writer.WriteString("name", data.Name);
    writer.WritePropertyName("type"); JsonSerializer.Serialize(writer, data.Type, options);
    writer.WritePropertyName("value"); JsonSerializer.Serialize(writer, data.Value, options);
    writer.WritePropertyName("signature"); JsonSerializer.Serialize(writer, data.Signature, options);
    // Flatten Properties — each entry rides as a top-level field.
    foreach (var (key, value) in data.Properties) {
        writer.WritePropertyName(key);
        JsonSerializer.Serialize(writer, value, options);
    }
    writer.WriteEndObject();
}
```

The Read side does the inverse — anything-not-reserved goes into Properties:

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
            case "name":      data.Name = reader.GetString() ?? ""; break;
            case "type":      data.Type = JsonSerializer.Deserialize<app.data.type>(ref reader, options); break;
            case "value":     data.SetValueDirect(ParseValue(ref reader, options)); break;
            case "signature": data.Signature = JsonSerializer.Deserialize<Signature>(ref reader, options); break;
            default:          data.Properties[key] = ParseValue(ref reader, options); break;
        }
    }
    throw new JsonException("Unterminated Data object");
}
```

Sign-if-missing walk skips Properties: the converter calls `EnsureSigned()` on the Data being written; it does NOT recurse into Properties values. (Properties values are primitives, not Data, so there's nothing to recurse into. The rule lands by construction.)

Variable expression parser learns `!`:

```
%x.field%   → x.Value navigation     (existing)
%x!key%     → x.Properties[key] navigation   (new)
%x.field.deep%   → x.Value.field.deep  (existing recursion)
%x!key.path%     → x.Properties[key] as primitive/dict, then dot-navigate within (new)
```

The `!` is a single token at the boundary between a variable name and the dereference. Subsequent `.` continues navigation into the dereferenced value (so `%response!cost.input%` reads `Properties["cost"]["input"]` if cost is a dict).

**Dependencies:** Stage 2 (the wire converter is what gets the flattening logic; the registered serializer is where Properties round-trip).

## Design

**Why Properties should not be List<Data>.** Today's `Properties : IList<Data>` would, after the sign-if-missing rule, drag every Property entry into the converter's sign walk. An LLM response with cost + tokens + latency would carry four signatures (outer + three Property-Datas). The wire shape becomes obviously wrong: Properties are metadata *about* the Data, not domain content — they shouldn't grow their own attestations. The Dictionary-of-primitives shape removes the entire question: there's no Data inside Properties for the walker to find.

**Why flat top-level instead of `properties: { ... }` nested.** Two reasons:

1. The LLM-response ergonomics. `{ name, type, value: "...", cost: 100, signature }` reads as a single object with cost as a sibling of value. The nested form `{ name, type, value: "...", properties: { cost: 100 }, signature }` adds a level of indirection that callers have to navigate. PLang's variable language gets the cleaner shape.
2. Forward compatibility. Receivers that don't know about a future top-level field (say PLang adds `traceId` later) carry it as a Property automatically — no breaking deserializer change. Nested-properties would either explode on unknown fields or need explicit "extra" handling.

**Why two operators (`.` vs `!`) and not collision detection.** The Value-namespace and Properties-namespace are *different stores* on the same Data. A typed `Data<User>` where User has `Kind` and the Data also has `Properties["kind"] = "admin"` — both exist; both are addressable; they mean different things. Operator-namespacing (dot vs bang) makes the call site explicit. Collision detection would force one to mask the other (which?) and break the symmetry. Two operators, two stores, no ambiguity.

**Reserved keys.** `name`, `type`, `value`, `signature` are reserved as Property keys because those names collide with the wire-shape's reserved top-level fields. Enforced at insertion (throws), with an optional Roslyn analyzer for compile-time catching of static literals. The check is case-insensitive to match the wire's case-insensitive key parsing.

**The `[Out]` discipline question deferred.** Earlier conversation raised whether Properties should be opt-in (`[Out]` on each entry) or opt-out (`[OutIgnore]`). Settled: all Properties cross the wire for this branch. Per-entry filtering (public/private, sensitive) is real future work but doesn't block this stage.

**Round-trip invariant.** A Data with `Properties["cost"] = 100`, written through `application/plang`, read back: `data.Properties["cost"]` returns `100` (boxed `int`). The deserializer doesn't lose type information because primitives' JSON encoding is faithful (`100` → JsonElement.GetInt64() → store as `long` or `int`). Document the type-promotion behaviour (int → long after JSON round-trip) so coder can write the test.

**Risks:**
- Callers reading `properties.PropertyName` (using the existing `IList<Data>` C# API surface — accessing list items by index) will break. The `IDictionary<string, object?>` surface is different. Compile errors guide the migration. Audit `Properties[...]` call sites.
- The `[JsonIgnore]` on Properties on `data/this.cs:187` may need to flip to `[JsonIgnore]` + `[In]` + `[Out]` to participate in Transport filter (same as Signature does). Coder confirms.
- Variable parser change: `%x!y%` is new syntax. Any existing variable expression containing `!` as a regular character (e.g., the negation prefix `%!flag%`) needs disambiguation. Today `%!flag%` is the boolean-negation operator on a variable — that's at the start of the expression, before any identifier. The new `!` is between identifier and key (`%x!y%`), so positionally distinct. Lex carefully.
- A Property with a structured value (e.g., `Properties["details"] = new Dictionary<string,object?>(...)`) is *allowed* (the `EnsureSupportedValue` admits dict + list of primitives), but Properties round-trip preserves it as the underlying JSON shape, not as a Data. Document that Properties values are *not* Data-instances on either side.

**What the coder verifies:**
- `Properties[key] = value` for each supported primitive type round-trips through `application/plang`.
- `Properties["name"] = "x"` throws; same for `type`, `value`, `signature` (reserved-key check).
- `Properties[unsupported-type]` throws with a clear message.
- Wire JSON of a Data with `Properties["cost"] = 100`: top-level `"cost": 100`, no nested `"properties"` envelope.
- Variable parser: `%response.text%` and `%response!cost%` resolve to different stores when both are populated.
- Receive a JSON with unknown top-level field (`traceId`): it lands in `Properties["traceId"]`, no error.
- Signing: a Data with Properties has exactly one signature (the outer); tampering with a Property value in the wire JSON invalidates the outer signature (proves canonicalization covers Properties).
