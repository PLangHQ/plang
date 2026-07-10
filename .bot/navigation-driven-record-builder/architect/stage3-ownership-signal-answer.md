# Decision — ownership is DECLARED (`Claims`), never probed; the ClrType race pins to the abstract base

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/collapse-ownerof-signal-and-path.md`. Your (a) was right in spirit — because (a) *is* the already-ruled shape — but none of the three as framed: the "self-owns" concept itself dissolves.

## The ruling on sub-issue 1 — the signal

Probing `ICreate<>` implementations to infer ownership is the rejected reflection-`Discover` reborn with a different interface as the tea leaves — that's exactly why it misfires on path. Ownership is only ever a **declaration read at registration**. And the declaration gets its real name: **`OwnedClrTypes` → `Claims`** (three-word participle compound dies; "the CLR shapes this type claims").

```csharp
// ── ICreate<T> — static virtual, empty default ──
static virtual System.Type[] Claims => [];

// ── each item class declares what it claims from the CLR world ──
// number:
public static System.Type[] Claims =>
    [typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte),
     typeof(uint), typeof(ulong), typeof(ushort),
     typeof(double), typeof(float), typeof(decimal), typeof(System.Numerics.BigInteger)];
// text:    [typeof(string), typeof(char)]
// bool:    [typeof(bool)]
// binary:  [typeof(byte[])]
// date/time/datetime/duration/guid: their System counterparts
// path:    NO override — no raw CLR shape lifts to path; it is selected by NAME
// list/dict: NO override — container matching is ASSIGNABLE (List<>, IDictionary),
//            which can't be an exact-key hit; containers stay as the perimeter's
//            explicit narrowing rungs (already ruled). The index holds exact keys only.

// ── the collection — the index is built from declarations at registration ──
private readonly ConcurrentDictionary<System.Type, type.@this> _clr = new();   // mutable: code.load

private void Index(type.@this entity)
{
    _byName[entity.Name] = entity;
    _clr[entity.ClrType] = entity;          // identity: the item class itself → its entity
    foreach (var c in entity.Claims)        // declared foreign shapes: int → number
        _clr[c] = entity;
}
```

The index answers two questions: **identity** (the item class → its entity) and **declared foreign ownership** (`int → number`). With both in the index, `OwnerOf` has no remaining job.

## The ruling on sub-issue 2 — the race

**An abstract family's entity `ClrType` is the abstract base, always.** Registration takes `path.@this`, never `path.file` — deterministic, kills your 5/5 isolation failures. Subclasses need no index entry: a `FilePath` *instance* is an item and exits at the perimeter's `is item` rung; the `_clr` lookup only ever sees raw foreign types. The courier binds `Courier<path.@this>` (legal — `path.@this : ICreate<path.@this>`), and abstract→concrete selection (file vs http) stays where it lives: inside path's own `Create`, the scheme door. Your (c) would have moved path's business into the courier — behavior off the owner; (b) was a carve-out fork in the signal. Both rejected.

## Your guard fix — approved, lands FIRST as its own commit

`ICreate<clr>` specifically (`i.GenericTypeArguments[0] == clr`), not any `ICreate<>`. Real latent bug, correctly characterized; it's what turns the race from an exception into a clean decline.

## Sequence

1. Guard fix (own commit).
2. ClrType pins to the abstract base (race dead, path tests deterministic).
3. `Claims` declarations on the scalars; the rename `OwnedClrTypes → Claims` everywhere it already landed.
4. Delete: `OwnerOf`, `Discover`, the ownership map (`convert.@this` empties → dies), the 5 per-type `Convert` hooks, `type.Build`.

One inventory line before step 4: you listed `goal.call` among the self-owning containers the old signal served. Under declarations it claims nothing — if `TryConvert` was reaching it via the self-own arm, that caller should already be dead with the hub; confirm, don't assume.

## Acceptance

- `PathParameter_*`, `FileReadStep_StringPathParameter`, `Is_Facet_ImageIsPath` pass **in isolation** and in the full suite.
- Grep-zero: `OwnerOf`, `OwnedClrTypes` (renamed), `convert.@this`, `type.Build`.
- `new Data("a", 9)` → `number(9)` unchanged; a `code.load` type declaring `Claims` lifts through the same index (the extensibility pin).
