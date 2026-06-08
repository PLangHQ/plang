# Stage 5: `data.Compare(other)` — the entry point

**Goal:** The one public comparison entry on `Data` — awaits both operands through the door, picks the driving type by rank, and runs the winner's sync `Compare`, in caller order, through the routing that already exists.
**Scope:** The `Compare` method on `Data` and its dispatch. Consumers (Stage 6) call it; the per-type `Compare` (Stage 4) does the work.
**Deliverables:**
- `public async ValueTask<Comparison> Compare(Data other)` on `Data`, returning the Stage-1 enum.
- Dispatch via `this.Type.Rank(other)` → the driving type → that type's `Compare`, in caller order, with no `Type.Name` switch.
**Dependencies:** Stage 4 (per-type rank + `Compare`). Closes the 2–6 green unit's compare half.

## Design

```csharp
// inside Data
public async ValueTask<Comparison> Compare(Data other)
{
    // Rank lives on the type; pass the WHOLE other Data (never other.Type).
    var driver = this.Type.Rank(other);          // the higher-ranked of the two types

    var a = await this.Value();                   // caller order: this is left, other is right
    var b = await other.Value();

    return driver.Compare(a, b);                  // sync; coerces the non-native side, orders left-vs-right
}
```

**Caller order — no sign flip.** `driver.Compare(a, b)` always orders `this`-value against `other`-value, so `Less` means `this < other` exactly as the caller asked, whether the driver came from `this` or `other`. Do **not** order winner-vs-loser and flip afterwards — that's a latent sign bug. Antisymmetry holds because `Rank` returns the same driver regardless of operand order.

**No new dispatch infrastructure, no `Type.Name` switch.** `driver` is reached through the same name→family routing the type entity already uses (`App.Type[Name]` → the family behaviour, the path `type.Convert` uses). Comparison is one more method on that behaviour. If you find yourself writing a name switch, the routing is already there — use it.

**Lazy stays honest.** The driving type is decided from the *types* (`Rank` needs no values), then both values are awaited. So a pending value is loaded only when the comparison genuinely happens, and ranking never forces a read. The two awaits are the only async hops; `Compare` itself is sync.
