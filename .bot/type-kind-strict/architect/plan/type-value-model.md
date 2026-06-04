# Topic — the `{name, kind, strict}` type value

This is the core data-model change. Everything else (text type, kind derivation, LLM representation) hangs off it.

## Today (post singular-namespaces merge)

The type descriptor is a promoted entity at `PLang/app/type/this.cs` (`app.type.@this`). It is both doors — `Data.Type` and `app.Type[name]` return the same shape — and it has absorbed the catalog (the old `app.builder.type.Entry` struct dissolved into it):

```csharp
public sealed class @this {              // app.type.@this
    [JsonPropertyName("name")] public string Value { get; }   // "string" | "image/jpeg" | "text/markdown"
    [JsonIgnore] public System.Type? ClrType => _clrType ?? Context?.App.Type.Clr(Value) ?? AppTypes.GetPrimitiveOrMime(Value);
    [JsonIgnore] public string? Kind => Context?.App.Format.KindOf(Value);   // FAMILY: "image","text" — note: family, not subtype
    [JsonIgnore] public bool Compressible => ...;
    public static @this Null { get; } = new("null", typeof(object));   // Data.Type is non-null now; this is the sentinel
    // folded catalog knowledge, lazily promoted via Promote():
    public IReadOnlyList<Field>? Fields { ... }   public IReadOnlyList<string>? Values { ... }
    public IReadOnlyList<Field>? Properties { ... }   public string? Shape { ... }
    public string? ConstructorSignature { ... }   public string? Example { ... }
    public string? Description { ... }   public IReadOnlyList<string>? Kinds { ... }   // VOCABULARY: "int","long",...
}
```

And `Data` still carries a *separate* `Kind` (`PLang/app/data/this.cs`):

```csharp
[JsonPropertyName("kind")] public string? Kind { get; set; }   // SUBTYPE: "jpg","md" — stamped at build by the Build() hook
```

So three different "kind"s are in play, two of them on this entity:

- `type.Kind` — the **family**, via `App.Format.KindOf` (`image`).
- `type.Kinds` — the advertised **vocabulary**, folded from the catalog (`int|long|decimal|double`).
- `Data.Kind` — the **subtype**, the thing a value actually is (`jpg`, `md`).

Plus `App.Type.Kinds` (`app.type.kind.@this`) — the build-hook **dispatcher**, a fourth use of the word. `type.Value` is still muddy (primitive name, full MIME, or family). And `variable.set` mints a variable copying `Value.Type` but **dropping `Value.Kind`** — a live bug.

## Target

The entity carries the three identity fields directly, keeping the folded catalog props as they are:

```csharp
public sealed class @this {              // app.type.@this
    public string Name { get; }      // canonical family/primitive: "text","number","image" — runtime-independent
    public string? Kind { get; }     // sub-format: "md","gif","int"; null = none
    public bool Strict { get; }      // default false
    // no public ClrType
    public bool Compressible => ...; // derived from Name's family
    public IReadOnlyList<string>? Kinds { ... }   // vocabulary — unchanged, stays for the LLM catalog
    // ... other folded catalog props unchanged (Fields/Values/Shape/Example/Description/ConstructorSignature) ...
}
```

- **`Name`** replaces `Value` as the canonical family/primitive — the `/` is already split into `Kind` by the time a `type` exists. `string` normalises to `text` here. (`[JsonPropertyName("name")]` already serialises it under `name`, so the wire key is unchanged.)
- **`Kind`** is what `Data.Kind` held (the subtype). `Data.Kind` is **deleted**; this entity is the single owner.
- The old family-`Kind` accessor (`App.Format.KindOf(Value)`) is **removed** — the family *is* the `Name`. `Compressible` re-derives from `Name`'s family.
- **`Kinds`** (the advertised vocabulary) is untouched — different concept, keep it.
- **`Strict`** is new.
- **`ClrType`** leaves the public surface. Name→CLR stays on `app.type.list.@this` (`App.Type.Get`/`.Clr`). It's already `[JsonIgnore]`; make it non-public/registry-internal. Three call-sites reach `.ClrType` outside `type/`/`data/`: `module/file/read.cs`, `module/variable/set.cs`, `module/settings/Sqlite.cs` — reroute to a registry call or a small `type.IsExit`-style helper.
- Rename the dispatcher `App.Type.Kinds` → `App.Type.KindHooks` (or similar) so `Kind` (subtype) / `Kinds` (vocabulary) / dispatcher stop colliding.

## Construction and tolerance

`type` gains a normalising factory that takes the structured form and is tolerant of the legacy/slash form:

- `type(name, kind?, strict?)` — canonicalises `name` (`string`→`text`) via the alias table, leaves `kind` to the canonicaliser (see [kind-derivation](kind-derivation-and-validation.md)).
- A single-string input that contains `/` (`"text/markdown"`) splits into `name`/`kind` — so a human writing a `.goal`, or a stray LLM emit, still works even though the taught form is structured. **Teach one form, accept both.** Multi-slash (`"a/b/c"`) splits on the first slash; the rest is the (free-string) kind — not an error.

This factory is where `string`→`text` and `markdown`→`md` normalisation lands at build (the LLM-facing path). The build pass calls it; the runtime reads the already-normalised result. Note the `Promote()` mechanism: catalog props resolve lazily by registry lookup on a stamped entity — adding `Name`/`Kind`/`Strict` must not disturb the `_foldLoaded`/`Context` invariant `Promote()` relies on.

## The wire — unchanged

The decision plang-types made stands: two flat keys, never a `type:kind` string. The entity collapses to one owner; the serialiser reads both off it:

```
{ "name": "...", "type": "image", "kind": "gif", "value": ... }
```

`Wire.Write` (`PLang/app/data/Wire.cs`) reads `Type.Value`/`Type.Name` → `type` key and `Type.Kind` → `kind` key. `Data.Kind`'s `[JsonPropertyName("kind")]` moves to being sourced from `Type.Kind`. (A thin `Data.Kind` accessor delegating to `Type?.Kind` is fine to reduce call-site churn — but not a stored field.) The `type.@this.Null` sentinel still serialises to nothing (no `"type":"null"`).

## Strict's seam — `IKindValidatable`

Strict enforcement is per-type knowledge — `image` can verify "is this a GIF" by sniffing magic bytes; `text` cannot verify "plain vs markdown." So a family that can verify its kind implements a small marker, sibling to `IBooleanResolvable` (`PLang/app/data/IBooleanResolvable.cs` → new `PLang/app/data/IKindValidatable.cs`):

```csharp
interface IKindValidatable { (bool ok, string? actualKind) ValidateKind(object value, string requiredKind); }
```

`image` (`PLang/app/type/image/`) implements it (sniff bytes → actual format, compare). `text` does not (strict for text degrades to "the kind name must be known"). The build validator (and, for `%var%`, the runtime) calls `ValidateKind` only when `strict` is true and the type implements the marker. Coder owns the exact shape — the intent: strict validation belongs on the type that can verify, never in `variable.set` or `build.validate` as a switch.

## Smells this fixes

- **#6 holds-a-reference-and-a-flat-copy** — `Data` no longer carries both `Type` and a flat `Kind` reachable through it.
- **The dropped-kind bug** — `variable.set` minting copies the whole `type` (kind included), not just the name.
- **The C# leak** — `ClrType` off the PLang surface.
- **The three-way "kind" collision** — resolved into `name` (family) / `kind` (subtype) / `Kinds` (vocabulary) / `KindHooks` (dispatcher).
