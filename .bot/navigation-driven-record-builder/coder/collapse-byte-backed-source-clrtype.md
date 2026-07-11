# Build/Convert death — done, one open cluster for architect (byte-backed `source` ClrType)

Coder → architect. Branch `navigation-driven-record-builder`. Work is **uncommitted** in the
working tree, tests at a clean re-baselined state (see numbers below).

## What landed (the collapse — settled, plan §72)

`type.Build` (the reflective value hub) and the entity `type.Convert` (its retype/throw
boundary) are **deleted**, folded into one door:

- **`type.Create(object? raw, context, string? format = null)`** — `PLang/app/type/this.cs`. The
  always-produce branchy door (your "Create always returns item"). Branches, in order:
  null→`@null`; variable-named type→`variable.Resolve`; **string/byte[] → lazy `source` (defer)**;
  native container→hold; built leaf→refine kind/template, else **unwrap to raw CLR + eager
  courier + `TryConvert` tail** (this absorbed `Convert`); raw CLR scalar→family lift thunk
  (`_create ??= Bind()`), then refine.
- **`type.Create(object? raw, data)`** — the eager courier (`Courier<T>`), **unchanged**. Used by
  `TryConvert`/infra `Convert` and by the door's leaf-retype. Kept eager on purpose — that's why
  the defer lives only on the `(…, context)` door, not here.
- Callers retargeted: `data` ctor (`data/this.cs:233`), `data.Declare` (`:283`), `data.FromRaw`
  (`:364`); `variable/set.cs:302` and `build/BuildResponse.Validate.cs:176` (the 2 `Convert`
  callers — they used it as a throw boundary; `Create`'s leaf-retype throws the same way).
- `source` now borns from the type entity: new ctor `source(value, type.@this, context, format)`
  (`item/source.cs`) — the door's string-branch and `FromRaw` share it, so `format` (the wire
  token) still flows.

**Laziness is intentional and correct** (Ingi ruled): all variables stay lazy — an authored/ingested
string or byte[] is just unread bytes until `.Value()` is called (a security property: sneaked-in
content is never parsed unless invoked). Do **not** make the door eager. An earlier `Lift`/eager
attempt was wrong and is fully reverted.

### Template re-stamp (Ingi's call — `internal set`)
The build detects a `%ref%` and marks the value's type `template="plang"` *after* the value is
built (`build/code/Default.cs:1002` `p.Declare(new type(…, "plang"))`). The old code re-stamped
inline via `new text(rt.ToString(), Template)` — which (a) only handled `text` so a `source` silently
dropped the flag, and (b) `ToString()` materialised (broke laziness). Fixed:
- `item.Template` is now `{ get; internal set; }` (`item/this.cs:253`).
- `source` unified its private `_template` into the `Template` property (`Mint`/`Ready`/`Cacheable`
  read `Template`).
- The refine sets it in place: `if (Template != null && refined.Template == null) refined.Template =
  Template;` (`type/this.cs`). A `source` keeps its undecoded bytes → stays lazy.
- `Templated` method idea (a virtual mirroring `Kinded`) was dropped in favor of `internal set`.

## Test state

Recorded baseline (`stage0-baseline.md`, commit `e32eafa52`): Types **21**, Data **35**, Wire **18**.
The `.pr`-fixture failures are out of scope (Ingi: fixture/plang-test category, not this plan).

After the collapse + fixes: **Types 40** (was 49 mid-work). Path, template, facet, and
non-binary validate clusters are green. **One cluster remains: 8 tests, all byte-backed
strict-validate:**

```
BuilderValidate_BuildReturnsOkWithTypeName_SetsTerminalVariableSetType
LiteralGifAsImageGifStrict_BuildsAndRunsClean
Run_StrictImageGifWithRuntimeVarResolvingToGif_Mints
Run_StrictImageGifWithRuntimeVarResolvingToPng_ThrowsTypedError
ValidateBuild_NotStrict_DoesNotValidate_EvenOnMismatch
ValidateBuild_StrictImageGifWithGifLiteral_ReturnsNull
ValidateBuild_StrictWithNoKind_ReturnsNull
VarAsImageGifStrict_BuildsClean_FailsAtRuntime
```

## The open problem — a byte-backed `source` reports a different ClrType than an eager value

Since the collapse, a `byte[]` borns **lazy** (a `source` declared `binary`) where before it was an
**eager** `binary` value. The two disagree on `Type.ClrType`:

```
binary.Mint()   (item/binary/this.cs:21)  → new("binary", typeof(byte[]))   // eager value PINS byte[]
source.Mint()   (item/source.cs)          → new(_type,_kind,_strict,Template) // no pinned _clrType
```

`type.ClrType` (`type/this.cs:159`) = `_clrType ?? Context?.App.Type.Clr(Name) ?? GetPrimitiveOrMime(Name)`.
For `"binary"`: `Clr("binary")` → `Get` → `ResolveType` → `ClrFromMime("binary")` = **null** (no `/`),
and `GetPrimitiveOrMime("binary")` = null too (`Aliases` has `"bytes"`→`byte[]`, not `"binary"`).

The failing check is `variable/set.cs:56-60`:
```csharp
var targetType = value.Type.ClrType;                          // "binary" source
if (targetType != null && !targetType.IsInstanceOfType(valueBacking))   // valueBacking = byte[]
    return $"Parameter 'Value' has type={value.Type.Name} but value is not a {…}: {valueBacking}";
```
Test error: `type=binary but value is not a binary: System.Byte[]`.

**⚠ Unresolved contradiction I could not settle by static reading:** by the resolution chain above,
`value.Type.ClrType` for a `binary` source should be **null** → the guard skips → no error. But the
test errors with a **non-null, non-`byte[]`** `targetType`. So something resolves `"binary"` to a
non-`byte[]` type at runtime that I can't see statically (candidate: the registry catalog entity for
`binary` carrying a pinned `binary.@this`, reached via `Context.App.Type.Clr` differently than
`ResolveType` suggests). **This needs a runtime probe to confirm before any fix** — I did not want to
guess a 4th time.

## What I tried that over-reached (do NOT repeat)

1. `ResolveType` switch `binary|image|audio|video → byte[]`: broke **path** (file.read returns
   image/table; forcing `image→byte[]` collapsed the family types). 40→47.
2. `Aliases["binary"] = typeof(byte[])`: a primitive alias **short-circuits family resolution** —
   `"binary"` stops resolving to `binary.@this`, breaking file.read/path/http/goal broadly. 40→55.

Both reverted. Lesson: `"binary"` is a **family** (has kinds, `binary.@this` wrapper); you cannot
make its *name* resolve to raw `byte[]` without destroying the family. The fix belongs on the
**value/source side or the validator**, not the name→CLR registry.

## Questions for architect

1. **How should a byte-backed family `source` report `Type.ClrType`** so a lazy value agrees with a
   materialised one? Candidate directions:
   - (a) `source.Mint()` pins `ClrType` from the raw it *holds* (`_value is byte[]` → `byte[]`) — local,
     the source knows its own backing, no registry change. Needs a type-entity ctor that takes
     name+kind+strict+template **and** a pinned clrType (only `new(name, clrType)` and
     `new(name, kind, strict, template)` exist today).
   - (b) Fix the strict-validate (`variable/set.cs:56-60`) to test *family membership* (does `byte[]`
     belong to `binary` via `OwnedClrTypes`) rather than `value.Type.ClrType.IsInstanceOfType` — the
     current check assumes `ClrType` == the raw backing type, which only held for eager values.
   - (c) something else — the eager-pins-`byte[]` / lazy-resolves-via-registry split is the smell;
     maybe both should route through one owner.

2. **`bytes` vs `binary` naming** (Ingi raised: "bytes should not be the family name, it should be
   binary"). Today `byte[]`'s *primitive* name is `"bytes"` (`primitive/this.cs:47,106`,
   `list/this.cs:175,488`, the set at `primitive/this.cs:142`) — 2 production + 7 test sites — while the
   *family* that owns `byte[]` is `binary` (`item/binary/this.Owns.cs`). Full canonical rename
   `bytes→binary`, or keep `bytes` as the primitive and reconcile only the family side? This overlaps
   Q1 and may be the real single-source-of-truth fix.

## Suggested next step

A one-shot runtime probe (test-only) printing `value.Type.Name` + `value.Type.ClrType` for
`new Data("Value", GifBytes, ctx)` would settle the contradiction in Q1 and tell us whether (a) or (b)
is correct. Happy to run it and report before implementing whichever direction you pick.
