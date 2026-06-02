# v10 — Stage 10: Value construction belongs to the types

## What
Dissolve `app.type.list.@this.TryConvertTo` (the ~400-line target-keyed god-switch in
`PLang/app/type/list/Conversion.cs`) into:
- **per-type `Convert` hooks** (the domain arms move onto the owning type),
- **two dispatch doors** sharing one dispatcher + one residual primitive leaf,
- the locale bug in `channel/serializer/Text.cs` dies.

The model + ownership map + three-phase order are settled (architect, with Ingi). Method
signatures + physical landing are mine.

## Baseline
C# 3822/3822. PLang 262 total / 259 pass / 3 fail (pre-existing event/Trigger fails) / 0 stale.
See `baseline-tests.md`.

## The two doors (my shapes)
- **Primary — `type.@this.Convert(value, ctx)`** (exists). Resolves family from the entity's
  `Name` (`App.Type[Name].ClrType` → family `@this` class), carries the entity's `Kind`, asks the
  hook (`App.Type.Conversions.Of`). On no-hook, delegates to the infra door with its `ClrType`.
- **Infra — `App.Type.Convert(value, clrTarget, ctx)`** (new instance method on `type.list.@this`,
  the registry). For callers holding only a CLR target. Resolves the owning family from `clrTarget`,
  derives the number-kind from the target precision, asks the hook; falls through to the shared
  dispatcher + residual leaf.

Both fall through to the **residual leaf** (bool/guid/enum/byte[]/ChangeType) + dispatcher
plumbing (null/nullable/assignable/data-wrap/list+dict element-recursion/string-ctor/complex
dict→json→deserialize) when no family owns the conversion.

### Design decision — family resolution must survive a null context/App
Several infra-door callers (`Data.GetValue<T>`, `Reconstruct.Walk`) historically converted with a
**null** context and no App. The plan's "Canonical→name→App.Type[name].ClrType" route needs an App.
So the dispatcher gets a small static **owner table** (`convert.@this.OwnerOf(clrTarget)`):
- `clrTarget` already declares a `Convert` hook (image/datetime/duration/path/GoalCall/text/number `@this`) → it IS the family class;
- raw CLR primitive a family owns the construction of → mapped: `int/long/decimal/double/float → number.@this` (+ kind), `string → text.@this`, `DateTimeOffset → datetime.@this`, `TimeSpan → duration.@this`.

This is dispatcher plumbing (a routing table), not a god-switch — it holds no per-type *logic*,
only "who owns this target". Keeps the infra door usable App-lessly; path/image arms still degrade
to the same null/Error they do today when context is null. (Deviation from the literal
"App.Type[name].ClrType" wording; same destination, more robust. Logged here per architect's
"signatures + landing are yours.")

## The json seam
`text.Convert` (target=text) stays structure→json-string. The **deserialize** direction
(json-string→generic structure) is a separate `text.@this` entry the **dispatcher** calls when it
sees a string source headed for a complex target — it parses to dict/JsonNode, then shapes toward
the target (existing structural round-trip). text never learns about `Goal`.

## Phases (shipped in order, all in this version)
1. **PoC bridge.** Add `number/image/path/datetime/duration/GoalCall` `Convert` hooks + extend
   `text` to deserialize. `TryConvertTo` asks the owning hook first, falls back to its arms. No
   call-site edits — all 15 benefit at once. Validate end-to-end.
2. **Drain.** Delete each proven arm from the switch. Delete `channel/serializer/Text.cs`
   `ConvertFromString` + `IsSimpleType`; route `Deserialize<T>` through the one converter,
   `IsSimpleType` → `IsPrimitive`. Locale bug dies.
3. **Re-door.** Replace static `TryConvertTo` with the dispatcher body + residual leaf + the two
   doors. Migrate the 15 call sites onto a door per the architect's table. Nothing on the old static.

## Tests
- Locale guard: `"3.14"` and a date string convert identically regardless of `CurrentCulture`
  (assert against the path that used to differ — `channel/serializer/Text.Deserialize<T>`).
- Each domain hook converts in isolation (unit) **and** through `Data.As<T>` (infra door reaches it).
- json-string → record via text-parse-then-shape.
- `bool`/`guid`/`enum` through the residual leaf.
- A hook-less type + non-json source still resolves via the plumbing fallback.
- PLang: existing `Types/*`, `TypeKindStrict/*`, `Serialization/*` stay green (they exercise the doors end-to-end).
