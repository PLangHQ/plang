# Builder green, `.pr` graph as hosts, one `Create` door — staged plan

**One plan, five stages; each stage lands as its own branch, in order.**
This file supersedes the earlier master plan (it had been patched into contradictions while the design settled; this is the clean rewrite, all decisions folded). Coder docs absorbed: `catalog-removal-and-create-unification.md`, `plan-review.md`, `plan-review-v2.md`. `from-source-spec.md` is superseded — records are hosts now, the navigate-pull record builder is dropped.

**You (coder/test-designer) own the final code and test shape.** Shapes below pin intent, not lines.

---

## Why

`plang build` is broken (three concrete failures, Stage 1). Root-causing them exposed six overlapping conversion mechanisms and two god-objects. Settled with Ingi (2026-07-08/09) as five small, ordered pieces — each independently shippable, builder working after the first.

---

## The settled model

1. **item ⟺ ICreate.** A type is a plang value iff it implements `ICreate` — it builds itself via `Create`. No `ICreate` → it's a **clr host**: built by deserialization through its kind, navigated/written by reflection.
2. **The `.pr` graph (goal/step/action/actions) are hosts.** Their `item.@this`/`ICreate` is the bridge (the goal Reader's own note) and goes. Plang carries them as `clr(goal)` / `clr<action>` (`Data<clr<goal>>`, `list<clr<action>>`); **C# properties stay plain** (`List<action>` — hosts hold hosts, `clr<>` only at the plang boundary).
3. **Hosts read/write through the `*` (reflection) kind, format-agnostic.** `Output` already does it (`Tagged.PropertiesFor`, View.Store → `[Store]`). `Read` and `Set` are its missing mirrors. The host's **C# class declaration is the single source of type knowledge** — the kind reflects `PropertyType` at the leaf. `JsonSerializer.Deserialize<goal>` and `GoalReadOptions` die (the goal ITypeReader ignored its format-agnostic reader and hardcoded STJ — the cheat).
4. **One construction door for items: `Create` — SYNC.** Dispatch is `T.Create(await Value(), this)`: the await sits **in front of** the door; `Create` receives a materialized item and works in memory (number parses its string, dict copies children). No async signature change anywhere. **No generic record builder** — every item writes its own `Create` (`permission` already does; a future record item writes its own too).
5. **kind owns cross-kind transforms** — `data.Convert(k)` → `kind.behavior.Convert` (**already async** — I/O-ish transforms live here): the html kind knows md→html; a kind that's just "build my type" (mp3→audio) delegates to the type's `Create`. `kind`/`strict` ride on `data.Type` — no extra params; strict enforced by timing (eager in `Create`, lazy at the value's load seam).
6. **Runtime `Create` door = a delegate on each `type.@this` entity.** One shared logic-free thunk on `type.@this` (`Builder<T>() => (v,d) => T.Create(v,d)`), closed **lazily per entity** on first use (`MakeGenericMethod` — the single reflective touch; a generator-emitted table is the later optimization). Users: the `as <type>` clause, kind→`Create` delegation, settings property binding. No static helper class; the entity owns closing its own builder.
7. **Registry = `list<type>`** (`app.type.list`), keyed name→entity index **on the collection**; `catalog` deleted. **Module discovery = `list<module>`** — views over the handler classes, reflection at the leaf, Fluid render for the LLM.
8. **Golden rule: the pattern never diverges.** Kind-varying construction lives on the kind (number's precision switches → follow-on). Writes always go through the value's own kind (`Kind.Set`) — `SetValueOnObject` (8 arms from outside) is the write-side obpv and dies.

---

## Stage 1 — `builder-green`

**Goal: `plang build` runs.** Done-when: the BuilderSanity repro builds and `cd Tests && plang --test` is green. Nothing else in this stage.

1. **data/reader routing** — `data/reader/this.cs:79-80`. Rule: String token → unchanged (text/plain; a full-match `%var%` keeps borning a `variable` in `type.Build`, `type/this.cs:265` — a different branch, untouched); non-String token **or** json-kind-declared → json → clr(json). Fixes `%plan.steps%` char-iteration (blocker-1, coder handoff "START HERE").
2. **`*`-kind `Set` + minimal `Read`** — the reflection kind gains `Set` (mirror of its `Navigate`): reflect `PropertyType` off the host, convert the incoming value, set it. For `set %goal.Steps[i].Actions% = %compileResult.actions%`: `PropertyType = List<action>` + incoming clr(json) → the **minimal `*`-kind `Read`** constructs the action hosts — `[Store]` props through the format-agnostic reader; `Parameters` (`List<Data>`) through the **data reader's `@schema:data` path**, so `%var%`-born/template/signing stay byte-identical. No STJ stepping stone (settled: straight to (b)).
3. **`goal.getTypes` List-lower** (blocker-2) — root-cause the `Data<list<dict>>` return whose native `List` hits the terminal LOWER door; fix the construction routing at that site.

Note: `SetValueOnObject` only *shrinks* here (the clr arm now covers the goal write); full deletion is Stage 3.

## Stage 2 — `pr-graph-hosts`

**Goal: goal/step/action/actions are plain C# hosts.**

- Drop `item.@this` + `ICreate` from the four classes.
- **Full `*`-kind `Read`**: `.pr` load reads the whole goal graph through the format-agnostic reader (extends Stage 1's minimal Read — same code, applied to the top of the tree). **DoD: sign-identical round-trip** — a param read via the new path is byte-identical (signature included) to today's.
- **Delete:** the goal/actions ITypeReaders (`goal/serializer/Reader.cs` + `Default.cs`, `actions/serializer/Reader.cs`), `Deserialize<goal>`, `GoalReadOptions` (`catalog/Conversion.cs:55-59`) + the goal dispatch (`:282`), the `.pr → goal-plang-type` MIME mapping.
- **Output consolidates:** hosts write through the reflection kind's `Output` (verified: identical `Tagged.PropertiesFor` loop to `item.OutputTagged`); dedupe `item.OutputTagged` (its only users are these hosts + test).
- `Data<goal>` → `Data<clr<goal>>` (`goal/getTypes.cs:34`), `Data<action>` → `Data<clr<action>>` (`environment/run.cs:15`); `action.AsData` wraps `clr<action>`. Precedent: `clr<app>` (goalsSave).
- **Bridge-item audit:** `snapshot`, `GoalCall`, `catalog/view`, `app` also declare item+ICreate — decide each by the rule: value or host.

## Stage 3 — `create-unification` (items only)

**Goal: one construction door; the reflective hub dies.**

- **Relocate** the per-type static `Convert` hooks → each type's own **sync** `Create` (number, text, datetime, date, time, duration, bool, binary, guid, image, path, dict, list; GoalCall per the Stage-2 audit). What changes: the door (hub→`Create`), the kind source (param→`data.Type.Kind`), the return convention (`context.Ok/Error`→`return`/`data.Fail`). Bodies move as-is.
- **`ICreate` default shrinks** to: pass-through → facet → decline. (Its `convert.OfStatic` tail, `type.Create(raw)` lift, and dict/list `Clr(typeof(TSelf))` branch all go.)
- **Entity `Create` delegate** on `type.@this` (model #6). `OwnerOf`/`_ownership` survives as the private index behind `type.Create(raw)` and the delegate bind — not a public door.
- **Delete:** `convert.OfStatic`/`Of`/`Invoke`/`Discover`; `type.Convert(value)` (`type/this.cs:187`); `TryConvert`'s construction stages (`catalog/Conversion.cs:128-522` — primitive `ChangeType` lowering stays in `item.Clr`; verify callers `type/this.cs:602`, `setting/this.cs:102` route through `Create` first); **`SetValueOnObject`, the whole method** — every write navigates to the target and calls its kind's `Set`.
- **`data.Convert(kind)` becomes real:** `kind.behavior.Convert` gains converters; a kind whose transform is "build my type" delegates to the entity `Create`.

## Stage 4 — `catalog-removal`

- **`app.type.list` = `list<type>`** + keyed name→entity index on the collection. **Runtime-hot** (`[name]→entity` is hit on every `.pr` read) — land and test apart from the reparent tail. Population stays lazy + runtime-extendable (module choice types, `code.load`); the `list`/`type` self-reference is data, harmless.
- Rehome `Kinds`/`Readers` to `app.type.*`; mechanical tail: `Renderers`/`KindHooks`/`Compares`/`Scheme`/`Choices` reparent (zero logic — the release valve, may trail as its own commits).
- Delete `type.catalog.@this`.

## Stage 5 — `module-discovery`

- `app.module.list : list<module>`; `module.Actions : list<action>` (the module-tree view — same action, class-level zoom); `action.Properties : list<type>` keyed by property name. **Views over the handler classes** — reflection once, at the `action` view leaf (unwrap `Data<T>`/`[Code]T` → plang type); consumers read `type.Name`, never a `System.Type`, never `GetTypeName` at a call site.
- Compile prompt = `Fluid(list<module>)` + types self-describing. **Delete** `Describe()`, `StepActions`, `BuildTypeEntries(modules)`. Check teaching parity (examples/defaults/return-types currently folded in `Describe()`).

## Follow-on — number kinds (separate branch, already scoped)

Number's precision switch family (`CoerceToKind` + serializer read/write switches + `FromDoubleAsKind`) dissolves into `type[number].kind[<k>]`, each precision owning its build. **Kind = storage type** (declaration → source → default `long`, a setting). **Precision = decimal places, edge-only** (max in every calculation; round only at output or explicit request). Overflow/mix policy (`Overflow.Promote`/`Precision.Error`) already settings-carried — stays. Full model: memory "The number model".

---

## Stays — do NOT remove

- `app/data/reader/this.cs` — only the `:79-80` routing line changes (Stage 1). `%var%`/template/signing byte-identical throughout.
- `item.Clr`/`ClrConvert` — the plang→CLR lower exit (a different direction). `dict.Clr`'s STJ for genuine map lowering; `list.Clr`.
- `type.Build`'s deferred-source born rule (full-match `%var%` → variable; string/bytes → lazy source).
- `type.Convert(string)`/`FromWire` — wire reconstruction (snapshot/crypto); verify callers in Stage 3, likely stays.
- `kind.Navigate`/`Enumerate`/`Load`/`Output` — untouched (Set/Read are additions).
- `Value<T>()` and the `ICreate` signature — **unchanged; `Create` stays sync.**
- `variable.set`'s three strict-enforcement moments (build / run / materialization).

---

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `*`-kind `Read`/`Set` | Mirrors of the existing `Output`/`Navigate`; host type knowledge from the class declaration, reflected at the leaf | Clean |
| sync per-type `Create` | Behavior on the type, no hub, no async ceremony; await in front of the door | Clean |
| entity `Create` delegate | On the owner (`type.@this` closes its own builder, lazily); no static helper class; one reflective touch | Clean |
| `list<type>` / `list<module>` | Registry/discovery ARE instances of the native collection; index on the owner | Clean |
| `SetValueOnObject` deleted | Writes via the target's own kind — one pattern, no divergence | Clean — removes the obpv |
| `clr<action>` boundary | Wrapper only where plang holds a host; C# graph stays plain | Clean |
| names | `Read`/`Set`/`Create`/`Convert` — single verbs on owners | Clean |
