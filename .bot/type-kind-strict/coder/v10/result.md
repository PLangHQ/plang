# v10 result — Stage 10: value construction onto the types

## Outcome
The central `app.type.list.@this.TryConvertTo` target-keyed god-switch is dissolved:
- Per-type `Convert` hooks own construction (`number`, `datetime`, `duration`, `path`,
  `image`, `GoalCall`); `text` already owned the serialize direction.
- The central converter is now a type-agnostic dispatcher (`internal static TryConvert`)
  + residual primitive leaf (bool/guid/enum/`ChangeType`/string-ctor/json-structural).
- Two public doors share it: primary `type.@this.Convert(value, ctx)` (entity in hand) and
  infra `App.Type.Convert(value, clrTarget, ctx, slot)` (CLR target in hand).
- The divergent twin (`channel/serializer/Text.cs` `ConvertFromString`/`IsSimpleType`,
  CurrentCulture) is gone → routed through the one converter → **locale bug fixed**.

## Dispatch
`convert.@this.OwnerOf(clrTarget)` is the routing table: raw CLR primitive → owning family
(+ number-precision kind), path subclass → `path`, any type declaring a `Convert` hook →
itself, else `(null,null)` → residual leaf + plumbing. Static / App-less so the infra door
also serves context-less callers.

## Tests (C# +11, all green; PLang unchanged from baseline)
`PLang.Tests/App/Types/ValueConversionHookTests.cs`:
- `TextChannel_TypedDeserialize_IsInvariantCulture_NotCurrentCulture` — the locale guard
  (`"3.14"` → `3.14m` under de-DE; the bug the twin caused).
- Each hook in isolation; each hook through the infra door.
- `InfraDoor_JsonString_ShapesToRecord`; `ResidualLeaf_BoolGuidEnum`;
  `PlumbingFallback_StringCtorType_NoHook`.

C# 3833/3833. PLang 259/262, 0 stale (3 pre-existing event/Trigger fails are the floor).

## Open item for review
The `json-text → generic structure` parse stays on `JsonString.ToJson` (a `string`
extension = text's CLR mate) rather than relocated onto a `text.@this.Convert` overload:
it is keyed by *source* not *target* (can't ride target-dispatch) and `ToJson` is already
the focused OBP owner. Record-shaping stays dispatcher target-shaping — the seam's split.
Flagged for the architect to confirm or redirect.
