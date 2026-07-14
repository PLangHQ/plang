# Cluster 3 ruling — home (1): `WriteReflected` lifts hosts at the reflection→wire boundary

Answer to `coder/cluster3-producer-shape-note.md`, approved by Ingi 2026-07-14. Good stop — the producer shape you found changes the answer, and neither of your fenced boundaries is crossed.

> **You own this.** Sketch traced against `87d27a1f5`; the code wins.

## The ruling, in one paragraph

`test.@this` is doing nothing wrong — the plan's host rule says C# properties hold hosts **plainly** (`goal Goal` is correct storage, and `HasSkipTag`/`Complete` need the raw host). The `clr<>` wrapper belongs "only at the plang boundary" — and `WriteReflected` IS that boundary, by its own doc: *"This is the reflection→wire boundary"* (item/this.cs:454-456), already carrying the BRIDGE collection case. So the walk learns one thing: **meet a host → lift it through the door → `clr(host)` writes its own tagged face.** This is NOT the rejected option (b): the writer (format layer) stays fail-closed and still throws on junk; only plang's own reflection walk gains the case, and the lift renders through `clr.Output`, so `[Out]`/`[Sensitive]` discipline is intact. Home (2) is rejected: lift-at-store breaks hosts-hold-hosts, and a parallel `[Out]` projection beside the raw field is the flat-copy smell repeated at every future host-holding item.

## The code — `item/this.cs`, `WriteReflected`'s `default` arm

```csharp
default:
    // A domain HOST riding an [Out] property (test.Goal) — not writer vocabulary.
    // The reflection→wire boundary lifts it through the door; clr(host).Output
    // renders its own tagged face ([Out]/[Sensitive] discipline intact).
    // Writer-vocabulary scalars (string/int/date/…) keep the writer's rendering.
    if (context != null && value is not System.IConvertible
        && context.App.Type[value.GetType()].ClrType == typeof(global::app.type.clr.@this))
    {
        await global::app.type.item.@this.Create(value, context).Output(writer, mode, context);
        return;
    }
    writer.Value(value); return;
```

Gate mechanics (they matter — keep them):

- `is not System.IConvertible` skips the registry hit for the common scalars (string/int/bool/DateTime).
- The rest ask **the three-rung identity door** we just built; only a clr-entity answer ("this is a host") lifts. `Guid`/`TimeSpan`/`DateTimeOffset` aren't IConvertible but ARE `_clr`-owned → the entity check routes them back to the writer.
- Scalar rendering stays byte-identical (untouched path); no hardcoded BCL list; no per-scalar allocation.
- Coverage is the CLASS of bug: any item holding an `[Out]` host — including elements inside the BRIDGE collection case, which loops through this same method — forever.

## Untouched, deliberately

- `writer.Value`'s throw — the fail-closed guard survives; a bare unregistered object reaching the WRITER still fails loudly.
- `test.@this` — field, tags, callers all as-is.

## Verify + pins

- **Confirm `goal` declares `[Out]` tags.** The wire now carries `clr(goal)`'s declared `[Out]` face; if goal is untagged, the Output contract makes that a loud error, and the fix is goal declaring a small `[Out]` summary face — the `app` precedent from the Stage-1 ruling. Check what the report actually reads off the goal and shape the face to that.
- The 5 writer-goal tests green (`GoalsTests`, `DiscoverActionTests`, `GoalMimeDeserializationTests`).
- `write out %!goal%` stays green (the computed path — regression guard).
- A bare *foreign* POCO on an `[Out]` slot still renders per the foreign-type rule (transparent dump); a deliberate junk write through the writer still throws `NormalizeUnexpectedLeafType`.
- The optional `%{Name}%` enrich at `data.Output` (clusters-2-3 answer) still recommended — next producer bug names itself.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| host case in `WriteReflected` | the boundary that owns the crossing gains the case; one home for the class of bug | ok |
| gate via the identity door | selection asked of the type system, not a hardcoded BCL list in a caller | ok |
| lift via `item.@this.Create` | one born door; no hand-rolled `new clr(...)` | ok |
| writer + test.@this untouched | fail-closed stays; hosts-hold-hosts stays | ok |
