# Topic — the `{name, kind, strict}` type value

This is the core data-model change. Everything else (text type, kind derivation, LLM representation) hangs off it.

## Today

`app.data.type` (in `PLang/app/data/this.cs`) is a flat string wrapper:

```csharp
public sealed class type {
    public string Value { get; }                 // "string" | "image/jpeg" | "text/markdown"
    public System.Type? ClrType => Context?.App.Types.Clr(Value) ?? AppTypes.GetPrimitiveOrMime(Value);
    public string? Kind => Context?.App.Formats.KindOf(Value);   // FAMILY: "image","text" — note: family, not subtype
    public bool Compressible => ...;
    public static type FromName(string) ...; public static type FromMime(string) ...;
}
```

And `Data` carries a *separate* `Kind`:

```csharp
[JsonPropertyName("kind")] public string? Kind { get; set; }   // SUBTYPE: "jpg","md" — stamped at build by the Build() hook
```

So "kind" means two different things on the same `Data`: `Type.Kind` is the family (`image`), `Data.Kind` is the subtype (`jpg`). And `type.Value` is muddy (primitive name, or full MIME, or family). `variable.set` mints a variable and copies `Value.Type` but **drops `Value.Kind`** — a live bug.

## Target

`type` carries the three fields directly:

```csharp
public sealed class type {
    public string Name { get; }      // canonical family/primitive: "text","number","image" — runtime-independent
    public string? Kind { get; }     // sub-format: "md","gif","int"; null = none
    public bool Strict { get; }      // default false
    // no public ClrType
    public bool Compressible => ...; // derived from Name's family (was derived from the old "Kind"=family)
}
```

- **`Name`** replaces `Value`. It is the canonical family/primitive name and nothing else — the `/` is already split off into `Kind` by the time a `type` exists. `string` normalises to `text` here.
- **`Kind`** is what `Data.Kind` used to hold (the subtype). `Data.Kind` is **deleted** — `type` is the single owner. The old `type.Kind` (family-from-formats) is gone; the family *is* the `Name`.
- **`Strict`** is new.
- **`ClrType`** leaves the public surface. Name→CLR resolution stays where it already lives — `app.types.@this.Get(name)` / `.Clr(name)`. Interior C# that needs the CLR type calls the registry, not `type.ClrType`. Blast radius is small: 6 references today, only `file/read.cs` (`read.Type?.ClrType.Exit()`) and the builder's `IsClrTypeName` sit outside `types/`/`data/`; both reroute to a registry call or a `type.IsExit`-style helper.

## Construction and tolerance

`type` gains a normalising factory that takes the structured form and is tolerant of the legacy/slash form:

- `type(name, kind?, strict?)` — canonicalises `name` (`string`→`text`) via the alias table, leaves `kind` to the canonicaliser (see [kind-derivation](kind-derivation-and-validation.md)).
- A single-string input that contains `/` (`"text/markdown"`) splits into `name`/`kind` — so a human writing a `.goal`, or a stray LLM emit, still works even though the taught form is structured. **Teach one form, accept both.**

This factory is where `string`→`text` and `markdown`→`md` normalisation lands at build (the LLM-facing path). The build pass calls it; the runtime reads the already-normalised result.

## The wire — unchanged

The decision plang-types already made stands: the wire writes two flat keys, never a `type:kind` string. The object collapses to one owner; the serialiser reads both off it:

```
{ "name": "...", "type": "image", "kind": "gif", "value": ... }
```

`Wire.Write` reads `Type.Name` → `type` key and `Type.Kind` → `kind` key. No new converter on `type`; it rides the existing hand-written envelope. `Data.Kind`'s `[JsonPropertyName("kind")]` moves to being sourced from `Type.Kind`. (If a thin `Data.Kind` accessor that delegates to `Type?.Kind` reduces call-site churn, that's fine — but it must not be a stored field.)

## Strict's seam — `IKindValidatable`

Strict enforcement is per-type runtime/build knowledge — `image` can verify "is this a GIF" by sniffing magic bytes; `text` cannot verify "plain vs markdown." So a family that can verify its kind implements a small marker:

```csharp
interface IKindValidatable { (bool ok, string? actualKind) ValidateKind(object value, string requiredKind); }
```

`image` implements it (sniff bytes → actual format, compare). `text` does not (strict for text degrades to "the kind name must be known"). The build validator (and, for `%var%`, the runtime) calls `ValidateKind` only when `strict` is true and the type implements the marker. Coder owns the exact shape — this is the intent: strict validation belongs on the type that can verify, never in `variable.set` or `build.validate` as a switch.

## Smells this fixes

- **#6 holds-a-reference-and-a-flat-copy** — `Data` no longer carries both `Type` and a flat `Kind` reachable through it.
- **The dropped-kind bug** — `variable.set` minting copies the whole `type` (kind included), not just the name.
- **The C# leak** — `ClrType` off the PLang surface.
