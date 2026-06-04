# coder — type-kind-strict (v1–v10)

## Version
v10 = Stage 10 (value construction onto the types; drain the central converter).
v8/v9 = codeanalyzer responses. v1–v7 = stages 1–9. See per-version dirs for history.

---

## v10 — Stage 10: value construction belongs to the types

### What this is
The 400-line target-keyed god-switch `app.type.list.@this.TryConvertTo` held *every*
type's construction knowledge (how `image` reads a path, how `duration` parses ISO-8601,
how `GoalCall` assembles, …). Adding a type meant editing the switch, and the knowledge
was duplicated in `channel/serializer/Text.cs` with a **CurrentCulture** parse — a live
locale bug (`"3.14"` → `314` under de-DE). Stage 10 moves each type's construction onto
the type (a `Convert` hook), leaves a type-agnostic dispatcher + residual primitive leaf,
and exposes two doors. The locale bug dies.

### What was done — three phases, all shipped, all green

**Phase 1 — hooks + bridge (additive).** Added per-type `Convert(object?, string? kind, ctx)`
hooks, discovered by the existing `app.type.convert.@this` dispatcher:
- `number/this.Convert.cs` — kind picks CLR precision (`int/long/decimal/double/float`),
  null kind derives via `Build`, invariant culture. Output is the CLR numeric, not the wrapper.
- `datetime/this.Convert.cs`, `duration/this.Convert.cs` — string → `DateTimeOffset` / `TimeSpan`.
- `path/this.Convert.cs` — string → path via the scheme registry.
- `image/this.Convert.cs` — path-string → lazy handle (declines raw `byte[]` → built at its
  own seam); proving instance for reference fundamentals.
- `goal/GoalCall.cs` — the ~50-line string/JsonElement/dict assembly arm, moved verbatim.
- `text` already owned structure→json-string (prior branch work).
`TryConvert` (the central converter) now asks the owning family's hook first
(`convert.@this.OwnerOf(target)` resolves family + number-kind), authoritative on success
and error, declining (null) falls through.

**Phase 2 — drain + kill the twin.** Deleted the path / image / TimeSpan / GoalCall arms
from the switch (their hooks own them now). Deleted `channel/serializer/Text.cs`
`ConvertFromString` + `IsSimpleType`; routed `Deserialize<T>` through the one converter
(invariant) and `IsSimpleType` → `AppTypes.IsPrimitive`. **Locale bug fixed.** Kept
empty-payload→`default` and text-payload→UTF-8-bytes as serializer-local concerns (a small
`FromText<T>` helper) — they are not universal conversions (`GetValue<byte[]>` must *reject*
a string, not UTF-8-encode it; that was a Decompress regression I hit and reverted from the
central leaf).

**Phase 3 — re-door.** The public static god-switch is gone: `TryConvertTo` → `internal
static TryConvert` (the shared dispatcher = plumbing + residual leaf, no per-type arms).
Added the public **infra door** `App.Type.Convert(value, clrTarget, ctx, slot)` →
`data.@this`. Migrated the cross-module call sites that hold a non-null context onto it
(`Data.As(typeName)`, `file/this.Operations` ×2, `builder/code/Default` validate + `ToGoalCall`).
Context-optional neighbors (`Data.As<T>`/`GetValue`, `Reconstruct`, the navigator, `Sqlite`,
`validateResponse`, `set`-validate) call the shared internal directly — they run without an
App, so an instance door isn't reachable; they are no longer touching a god-switch.

### Key decisions (mine; architect set the model)
- **`convert.@this.OwnerOf` routing table** (static): raw CLR primitive → family (`int→number`
  + kind, `string→text`, `DateTimeOffset→datetime`, `TimeSpan→duration`), path subclass → path,
  any type declaring a `Convert` hook → itself. App-less so the infra door serves context-less
  callers. Deviation from the literal "Canonical→name→App.Type[name].ClrType" (needs an App);
  same destination, more robust.
- **`WithSlot`** enriches an owning-hook error with the binding slot name — the hooks don't
  carry it, the door does; preserves action-param bind-error quality.
- **json seam left as-is, flagged.** `json-text → generic structure` already lives on
  `JsonString.ToJson` (a `string` extension = text's CLR mate, "the string owns its conversion");
  the dispatcher uses it. I did not relocate it onto a new `text.@this.Convert` overload — it's
  keyed by *source* not *target* so it can't ride target-dispatch, and `ToJson` is already the
  focused OBP owner. The record-shaping (structure→target) stays dispatcher target-shaping, which
  *is* the seam's split. **Architect: confirm acceptable, or say to relocate onto text.**

### Code example (the pattern — every hook looks like this)
```csharp
// PLang/app/type/duration/this.Convert.cs — the type owns its own construction.
public static global::app.data.@this Convert(object? value, string? kind,
    global::app.actor.context.@this context)
{
    switch (value)
    {
        case null:            return data.@this.Ok(value);
        case System.TimeSpan: return data.@this.Ok(value);
        case string s:
            var parsed = Resolve(s, context);            // reuse the type's own parser
            return parsed != null ? data.@this.Ok(parsed.Value)
                                  : data.@this.FromError(new error.Error("…", "DurationParseFailed", 400));
        default: return data.@this.FromError(new error.Error("…", "DurationConversionFailed", 400));
    }
}
```
The central converter no longer knows how a duration parses — it asks:
```csharp
var (family, kind) = convert.@this.OwnerOf(targetType);     // TimeSpan → duration.@this
var owned = context.App.Type.Conversions.Of(family, value, kind, context);
if (owned is { Success: true }) return (owned.Value, null);  // the type built it
```

### Verification
Clean rebuild. **C# 3833/3833** (+11 new stage-10 tests: locale guard, each hook in
isolation + through the infra door, json-string→record, bool/guid/enum residual leaf,
hook-less string-ctor plumbing fallback). **PLang 259/262, 0 stale** — identical to baseline;
the 3 fails (`Channels/Events/AddOnAsk`, `AddBeforeWrite`, `Modules/Event/Basic`) are
pre-existing event/Trigger-merge failures, not this stage.

### Files
- New hooks: `type/number/this.Convert.cs`, `type/datetime/this.Convert.cs`,
  `type/duration/this.Convert.cs`, `type/path/this.Convert.cs`, `type/image/this.Convert.cs`;
  `goal/GoalCall.cs` (+Convert).
- Dispatcher: `type/convert/this.cs` (+`OwnerOf`); `type/list/Conversion.cs` (hook bridge,
  drained arms, infra door `Convert`, `TryConvertTo`→internal `TryConvert`, `WithSlot`).
- Twin: `channel/serializer/Text.cs` (ConvertFromString/IsSimpleType gone → one converter).
- Call sites: `type/this.cs`, `data/this.cs`, `path/file/this.Operations.cs`,
  `builder/code/Default.cs`, plus rename-only touch in `builder/validateResponse.cs`,
  `module/test/discover.cs`, `variable/set.cs`, `variable/list/this.cs`,
  `path/this.JsonConverter.cs`.
- Tests: `PLang.Tests/App/Types/ValueConversionHookTests.cs`; facade + two direct-caller
  test files updated for the rename.
