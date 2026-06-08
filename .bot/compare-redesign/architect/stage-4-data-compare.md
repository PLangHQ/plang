# Stage 4: `Data.Compare(other)` — the entry point

**Goal:** Wire the one public comparison entry on `Data` that picks the winner by rank, awaits both values through the door, and runs the winner's sync ordering core — through the routing that already exists, with no `Type.Name` switch.
**Scope:** The `Compare` method on `Data` and its dispatch. Excludes the consumers that call it (Stage 5) and deleting the old path (Stage 6).
**Deliverables:**
- `public async ValueTask<Comparison> Compare(Data other)` on `Data`, returning the Stage-1 enum.
- Dispatch that uses `this.Type.Rank(other)` (Stage 3) to pick the **driving type**, then orders in caller operand order, via the existing name→family routing.
**Dependencies:** Stage 3 (per-type rank + ordering core).

## Design

```csharp
// inside Data
public async ValueTask<Comparison> Compare(Data other)
{
    // Rank lives on the type, not on Data. Pass the WHOLE other Data (never other.Type);
    // this.Type returns the driving type — the higher-ranked of the two (this.Type or other.Type).
    var driver = this.Type.Rank(other);

    var a = await this.Value();    // caller order preserved: this is the left operand, other the right
    var b = await other.Value();

    // The driver coerces whichever side isn't already its kind, and orders left-vs-right (this vs other).
    return driver.Order(a, b);
}
```

**Order in caller order — no sign flip.** `driver.Order(a, b)` always orders `this`-value against `other`-value, so a `Less` means `this < other` exactly as the caller asked, whether the driver came from `this` or from `other`. Do **not** order winner-vs-loser and try to flip afterwards — that's a latent sign bug. The driver is the higher-ranked type, so exactly one side is natively its kind (or both, if same type); `Order` coerces the other side and compares in the given `(a, b)` order. Antisymmetry holds because `Rank` returns the same driving type regardless of which operand called `Compare`.

**No new dispatch infrastructure.** `driver` is the family behaviour reached through the same name→family path the type entity already uses for `type.Convert` (`App.Type[Name].ClrType` → the family behaviour). Comparison is one more method on that behaviour — there is **no** new compare registry and **no** `Type.Name == "..."` switch anywhere in the path. If you find yourself writing a name switch, the routing you need is already there; use it.

**Order of operations matters for laziness.** The driving type is decided from the *types* (`Rank` needs no values), then both values are awaited. So a pending value is only loaded once the comparison is genuinely happening, and ranking never forces a read. The two awaits are the only async hops; everything after (`Order`) is sync.
