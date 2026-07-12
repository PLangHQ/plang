# Decision — the source holds its declared entity whole; `Mint()` and `item.Template` die; validation leaves `variable/set`

**From:** architect. **Settled with Ingi (2026-07-12).** Answers `coder/collapse-byte-backed-source-clrtype.md` (the 8 byte-backed strict-validate reds + your Q1/Q2), and rules the Template re-stamp shape from your collapse doc. Three pieces, one root: **the type is born at value birth — we always had the info; the code was throwing it away and rebuilding it from notes.**

Your contradiction first, settled by static reading (no probe needed): your `ClrType` trace for a `binary` source stopped at `ClrFromMime("binary") = null`, but the branch above it answers first — the assembly scan (`Registry.cs:187-203`) registers every value `@this` under its inferred name, so `Context.App.Type.Clr("binary")` = **`typeof(binary.@this)`, the wrapper class**. `typeof(binary.@this).IsInstanceOfType(byte[])` = false → exactly your error text. The validator was comparing a *backing* against an *identity* — it only ever "worked" against eager values because `binary.Mint()` happened to pin `typeof(byte[])`. Neither your (a) nor (b) as framed: (a) pins the split-personality deeper (and `image.Mint` pins nothing, so the image/gif half of the cluster stays red); (b)'s ownership lookup breaks the same tests the other way (`byte[]`'s owner is `binary`, *only* binary — image claims just its identity, verified `this.Owns.cs`). The fix is (c): the split is the smell, and it dissolves below.

## 1. The source holds its declared entity WHOLE (the flat-copy dies)

Today the entity ctor receives the whole declaration and immediately shreds it (`source.cs:48-51`: `type.Name, type.Kind?.Name, type.Strict, type.Template` → four scalar mirrors), then `Mint()` reassembles a degraded copy (`new type(_type, _kind, _strict, Template)` — fresh entity, no Context, late-stamped by Data afterwards). Ingi: "Mint is asking what type will you become — we have that info at creation."

```csharp
// item/source.cs
private readonly object _value;
private readonly global::app.type.@this _type;      // the declaration, WHOLE — born with it
private readonly string _format;
// ✗ dead: the string _type, _kind, _strict, the Template copy — all shreds of _type
// ✗ dead: the string-based ctor (typeName, kind, strict, format, template) — loose strings
//         belong on a type entity; a caller holding strings builds the entity first

public source(object value, global::app.type.@this type, actor.context.@this context, string? format = null)
{
    _value  = value ?? throw new System.ArgumentNullException(nameof(value));
    Context = context ?? throw new System.ArgumentNullException(nameof(context));
    _type   = type;
    _format = format ?? type.RawFormat(value, context);
    // the authored-template gate, read off the DECLARATION at birth — immutable from here:
    if (type.Template != null && value is string reference
        && global::app.data.@this.TryFullVarMatch(reference, out _))
        IsVariable = true;
}

public override global::app.type.@this Type => _type;   // was Mint()'s reconstruction
public string Format => _format;                          // birth state the re-birth arm preserves (like Raw)
// Ready/Cacheable/reader lookup read _type.Name / _type.Kind?.Name / _type.Template —
// same values, off the object they were born on.
```

The `c:\file.txt` chain is untouched: the source declares "path", first touch parses through the reader for (`_type.Name`, `_type.Kind?.Name`), path's own Scheme door picks file-vs-http from content. The declaration is complete at birth; the *become* stays lazy — which is also why pinning a ClrType at source-birth (your (a)) was structurally wrong: for an abstract family the concrete CLR shape genuinely isn't known yet.

## 2. `Mint()` dies into the `Type` property; `item.Template` dies

**Mint** was a property-shaped question behind a method name, and `data.Type` already forwards to `_item.Type` — Mint is the middleman hook under it. It folds in:

```csharp
// item/this.cs — DELETE:
protected internal virtual global::app.type.@this Mint() => …        // ✗ dies
// the Type property stays the one door; its chain accumulation keeps working —
// per-type Mint() overrides RENAME into Type overrides, derivation logic untouched:

// binary/this.cs (mechanical example; image/number/text/… identical moves):
public override global::app.type.@this Type
    => new("binary", typeof(byte[])) { Kind = Kind is { } k ? new global::app.type.kind.@this(k) : null };

// caller sweep (one-word swap): data/this.cs:433 (.Mint().Kind → .Type.Kind),
// type/this.cs:287/:317, ICreate.cs:83, item/this.cs:107/:206-208/:263, date/this.cs:52
```

Eager values still *derive* their entity — correct, their type is intrinsic. Only the source was reconstructing a declaration it had been handed.

**`item.Template { get; internal set; }` dies** (`item/this.cs:253`). Settled with Ingi: Template is a **builder judgement about the slot** — the same family as Name/Kind/Strict — so it lives on the declaration (`type.Template`), not on the value. (Contrast `format`: the deserializer's fact — machinery, rides the read call. Different origins, different homes.) Your `internal set` was the workaround for the info the old source shredded — with the entity held whole, the reason it exists is gone, and a *mutable* "trust this content" flag on a security gate can't stay regardless: birth-immutable or it isn't a gate. Consumers (`HasVariableReference`, output.write's peek-time read, your Cacheable/Ready unification) read `value.Type.Template` — for a deferred value that forwards to the held entity. **Inventory the read sites**: the flag is consumed at the source seam (a stamped source re-renders per read, `Cacheable=false`, so the source persists as the value) — if you find a consumer that genuinely needs `Template` off a *materialized* value, stop and surface.

**The re-stamp becomes a re-birth through the door.** `Declare` is already right and does not change (`data/this.cs:276` — `_item = declared.Create(_item, _context)`, the one door). The door's ladder gains one arm, replacing the leaf-refine re-stamp (`refined.Template = Template` — ✗ dies):

```csharp
// type.Create(object? raw, ctx, format) — before the generic leaf handling:
if (raw is item.source s)
    return new item.source(s.Raw, this, ctx, s.Format);   // re-declaration = re-birth over the same unread raw
```

`Default.cs:1002` stays byte-identical: `p.Declare(new type(…, "plang"))` now does the whole job — Declare → door → source arm → new source born with the stamped declaration. Sets rebind, values immutable, laziness intact (the raw is unread bytes), all types covered (the old inline re-stamp was text-only). Do NOT route this through `SetValueDirect` — it's marked transitional debt, do-not-add-callers, and bypassing the door here would be the exact obpv this branch exists to kill. (Re-birth on an unchanged declaration is harmless — cheap mint over unread raw; add a reference-equality skip if it bothers you. Your call.)

## 3. Validation leaves `variable/set` — the value system answers, polymorphically

Ingi: the action should be dumb (`variable/set` calls `context.variable.set`, minimal logic even there); the item owns its own responsibility; and **`ClrType` reaching into a plang-level check is the underlying smell — CLR facts belong at BCL API edges only.** So the type-match check (`variable/set.cs:51-60`) leaves the action; the question "do you satisfy your declaration?" is asked of the value system and answered by type NAMES, never CLR:

- a **materialized value** answers by comparing its own `Type.Name` against the declared name (a born `number` under a `text` declaration → build error, as today);
- a **source answers by being a source**: it IS the declaration, unparsed — nothing has become anything yet; valid by construction. Content truth has two owners already in place: the strict **kind** probe at build (`IKindValidatable` magic bytes — untouched, it's the kind's job) and the **first-touch parse** at runtime (strict at the load seam, fails loud into the error channel).

That maps the 8 reds: gif-literal-strict → kind probe passes; png-under-gif at runtime → load-seam typed error; not-strict / strict-no-kind → no check fires; the deferred type-match → source's own answer (skip). You own the final shape of where the ask lands (the data/value seam the action calls) — the ruling is only: not in the action, not via ClrType, the source answers for itself.

## Acceptance

- The 8 byte-backed strict-validate tests green; Types suite ≥ 40 baseline held.
- Grep-zero: `Mint()` (property everywhere), `item.@this.Template` setter, the refine re-stamp line, `ClrType` in `variable/set.cs`.
- A parameter whose literal carries a `%var%` still borns a live template after the build's `Declare` (the re-birth path pinned by a test).
- The `c:\file.txt as path` chain: source → first-touch → Scheme → `path.file` unchanged.
