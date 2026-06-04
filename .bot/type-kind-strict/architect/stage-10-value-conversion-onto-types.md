# Stage 10: Value construction belongs to the types — drain the central converter

> **Coder: you own the final shape.** The ownership map, the dispatch model, and the three-phase order below are settled (with Ingi). The method signatures, where each domain arm's logic physically lands inside its type, and the exact residual-leaf shape are yours. **Do not stop at Phase 1** — the proof-of-concept bridge is explicitly a scaffold; the stage is done only when the central switch is gone and the call sites are on the two doors. If a phase reveals the map is wrong, say so and we adjust — don't quietly leave it half-migrated (that's the exact failure this stage exists to correct).

**Goal:** A type owns how a value of itself is built from raw input. The 400-line central target-type switch (`app.type.list.@this.TryConvertTo`) dissolves into two thin dispatch doors plus a small type-agnostic primitive leaf; every conversion call site reaches per-type knowledge instead of a god-switch.
**Scope:** Included — `Convert` hooks on `number`, `text` (extend to the deserialize direction), `image`, `path`, `datetime`, `duration`, `GoalCall`; the two dispatch doors; migrating the ~15 `TryConvertTo`/`ConvertTo` call sites; deleting the divergent twin in `channel/serializer/Text.cs`; deleting the drained arms. Excluded — making `number.@this` the *flowing* value (it stays the converter's owner; the output is still a CLR `int`/`long`/… so nothing downstream changes); `list`/`dict` family hooks (element-recursion stays dispatcher plumbing); any new value-shape behavior.
**Dependencies:** Builds on the `type.@this.Convert` + `app.type.convert.@this.Of` dispatcher and `text.Convert` already on this branch. Overlaps stage 9 (`image` path-handle): `image.Convert` *is* the path-backed lazy handle from stage 9 surfaced as the type's own hook — this stage unifies stage 9's `variable.set` carve-out and the converter's `string→image` arm into one place.

## Why

`text→json` proved the model — a type knows how to make itself — but `text` is the only type that owns its construction. Everything else still routes through `TryConvertTo`, a single switch keyed by target type that holds *every* type's construction knowledge: how `image` reads a path, how `path` resolves a scheme, how `GoalCall` assembles, how `duration` parses ISO-8601. Adding a type means editing the switch. Worse, the knowledge is also *duplicated* — `channel/serializer/Text.cs` reimplements the string→primitive arm with `CurrentCulture` parsing where the switch uses `InvariantCulture`, a live locale bug. This stage finishes what `text` started: construction knowledge lives on each type; the central function shrinks to a dispatcher plus the irreducible CLR-primitive leaf.

## The dispatch model (settled)

Two doors, one dispatcher, one small leaf. The static `app.type.list.@this.TryConvertTo` god-class **goes away** — its body splits: domain arms onto the types, the primitive/structural plumbing into a shared internal.

- **Primary door — `type.@this.Convert(value, ctx)`.** Used anywhere a PLang `type` entity is in hand (e.g. `variable.set`). Already exists; routes via `convert.@this.Of(familyClass, value, kind, ctx)`.
- **Infra door — `App.Type.Convert(value, clrTarget, ctx)`.** Used by callers that only hold a CLR target (`Data.As<T>`, wire reconstruct, the variable navigator, Sqlite, builder validators). Resolves the family from `clrTarget` (the reverse lookup already exists — `primitive.@this.Canonical` → name → `App.Type[name].ClrType`), then asks the same hook.
- Both fall through to the **residual leaf** when no family owns the conversion.

## Ownership map (settled)

| Conversion | Owner | Was (switch arm) |
|---|---|---|
| `int`/`long`/`decimal`/`double`/`float` | **`number`** — `Convert` keyed by kind (kind picks precision; `null` kind derives via `Build`); invariant culture lives here. Output is the CLR numeric, not `number.@this`. | `Convert.ChangeType` primitive arm |
| json-text ↔ structure (both directions) | **`text`** — already serializes structure→json-string; add json-string→generic structure (dict/`JsonNode`). See the seam below. | `String→JsonNode`, `string→complex via Deserialize`, `dict/JsonElement/JsonNode/IList→serialize→deserialize` |
| path-string → lazy handle | **`image`** (proving instance; audio/video same shape) | "reference fundamental backed by path" arm |
| string → `path` (scheme resolution) | **`path`** | "Path: route through scheme registry" arm |
| string → date/time | **`datetime`** | (DateTime via `ChangeType`) |
| string → duration (ISO-8601 / .NET) | **`duration`** | `TimeSpan` arm |
| string/JsonElement/dict → `GoalCall` | **`GoalCall`** (the ~50-line arm is pure domain assembly) | `GoalCall` arm |
| `bool` / `guid` / `enum` / `byte[]` (utf8) | **residual leaf** — genuine CLR primitives, no PLang family; a thin shared internal, *not* a smell | `ChangeType` / `Guid` ctor / `Enum.TryParse` / utf8 |
| plumbing — null/nullable unwrap, assignable passthrough, `data.@this` wrap, list/dict **element-recursion**, generic string-ctor reflection fallback | **dispatcher** — type-agnostic; element-recursion re-dispatches per element | (unchanged) |

## The json seam (the one wrinkle, settled)

Dispatch is keyed by the **target** type, but "json-string → `Goal`" is keyed by the **source**. So split the one operation:

1. **`text` owns json-text ↔ generic structure.** `text/json`.Convert parses the string to a *generic* structured value (dict / `JsonNode`) and serializes a structure back to a string. That is text's only json knowledge — it never has to know about `Goal`.
2. **Target-shaping is separate.** "generic structure → `Goal`" is the target type's job (or, for a plain record with no hook, the dispatcher's structural round-trip). When the dispatcher sees a *string* source headed for a complex target, it asks `text` to parse first, then shapes the result toward the target.

## Phases — planned together, shipped in order

1. **PoC bridge (scaffold).** `TryConvertTo` asks the type hook first (`convert.Of` resolved from the target), falls back to its existing arms. Write `number.Convert` + the domain hooks (`image`/`path`/`datetime`/`duration`/`GoalCall`) and extend `text.Convert` to the deserialize direction. Validate every caller end-to-end — all 15 sites benefit at once with no edits yet. This proves the hooks before anything is deleted.
2. **Drain.** As each hook proves out, delete its arm from the switch. Delete the divergent twin — `channel/serializer/Text.cs::ConvertFromString` and `IsSimpleType`; route its `Deserialize<T>` through the one converter and replace `IsSimpleType` with `IsPrimitive`. **The locale bug dies here.**
3. **Re-door.** Replace the static `TryConvertTo` with the two dispatch doors; migrate the call sites per the table below. What remains central is only the residual primitive leaf + dispatcher plumbing.

## Call-site disposition (leaf trace — every site accounted for before coding)

| Call site | Holds | Disposition |
|---|---|---|
| `module/variable/set.cs:218` | `type` entity | already on the primary door (`typeEntity.Convert`) — keep |
| `module/variable/set.cs:59` (build-validate) | `type` entity | primary door |
| `data/this.cs:817` (`As<T>`), `:301`, `:319`, `:482-483` | CLR `T` | infra door |
| `data/this.Reconstruct.cs:50`, `:73`, `:112` | CLR target | infra door |
| `type/this.cs:169`, `:277` | family CLR | becomes dispatcher internal (this *is* the door's body) |
| `type/path/file/this.Operations.cs:78`, `:117` | materialized CLR | infra door |
| `variable/list/this.cs:367`, `:386`, `:404`, `:481` (navigator) | CLR slot/prop type | infra door |
| `settings/Sqlite.cs:326` | `clrType` | infra door |
| `module/builder/code/Default.cs:959`; `:1115` (`ConvertTo<GoalCall>`) | CLR target / GoalCall | infra door (`:1115` → `GoalCall` hook) |
| `module/builder/validateResponse.cs:241` | CLR target | infra door |
| `module/test/discover.cs` | CLR target | infra door |

## Deliverables

- `Convert` hooks: `number`, `image`, `path`, `datetime`, `duration`, `GoalCall`; `text` extended to json-string→structure.
- The two dispatch doors (`type.@this.Convert` primary — extend if needed; `App.Type.Convert(value, clrTarget, ctx)` infra) sharing `convert.@this.Of` + the residual leaf.
- Static `app.type.list.@this.TryConvertTo` removed; its domain arms gone, plumbing/leaf relocated to the shared internal.
- `channel/serializer/Text.cs::ConvertFromString` + `IsSimpleType` deleted; routed through the one converter / `IsPrimitive`.
- All call sites in the table on a door, none on the old static.
- Tests: `"3.14"` and a date string convert identically regardless of culture (locale-bug regression guard — assert against the path that used to differ); each domain hook converts in isolation (unit) *and* through `Data.As<T>` (the infra door reaches it); a json-string converts to a record via text-parse-then-shape; `bool`/`guid`/`enum` still convert through the residual leaf; a type with no hook and a non-json source still resolves via the plumbing fallback.

## Non-goals / parked

- **`number.@this` as the flowing value.** Out of scope — `number` owns the *conversion logic*, output stays a CLR primitive. Whether the wrapper becomes what's stored is a separate decision.
- **`list`/`dict` family hooks.** The element-recursion is type-agnostic dispatch plumbing; it re-dispatches each element to the right owner and stays in the dispatcher.
- **`bool`/`guid`/`enum`/`byte[]` family classes.** Deliberately not created — these are genuine CLR primitives; a thin shared leaf is the correct home, not the smell.
