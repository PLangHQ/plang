# Builder green, `.pr` graph as hosts, one `Create` door — staged plan

**One plan, six stages (0–5); each stage lands as its own branch, in order.**
This file supersedes the earlier master plan (it had been patched into contradictions while the design settled; this is the clean rewrite, all decisions folded). Coder docs absorbed: `catalog-removal-and-create-unification.md`, `plan-review.md`, `plan-review-v2.md`. `from-source-spec.md` is superseded — records are hosts now, the navigate-pull record builder is dropped.

**You (coder/test-designer) own the final code and test shape.** Shapes below pin intent, not lines.

---

## Why

**The main goal: we do the same thing 2–4 different ways, and this branch removes that.** Construction alone has six overlapping mechanisms; type identity and module discovery each have their own god-object. One way for each thing — construction via `Create`, hosts via the `*` kind, the registry as `list<type>`, discovery as `list<module>`.

The broken `plang build` was the trigger (it exposed the duplication), and its fix is a *consequence* of the unification. Verification: the C# unit suite guarded against a **recorded baseline** (many tests are already red — coder's first step is to run the suite and write the number down), a **unit test pinning the original build error**, and the Sanity-goals `plang build` progressing past that error (likely to the next one — that's expected, not failure).

**Removal is tracked in code:** everything on the removal list gets `[Obsolete]` at branch start — every remaining caller is then a live compiler warning, a migration tracker that shrinks stage by stage. Each stage deletes its own; the final stage sweeps what's left. Whatever can't die yet stays marked — known, findable future debt instead of forgotten code.

---

## The settled model

1. **item ⟺ ICreate.** A type is a plang value iff it implements `ICreate` — it builds itself via `Create`. No `ICreate` → it's a **clr host**: built by deserialization through its kind, navigated/written by reflection.
2. **The `.pr` graph (goal/step/action/actions) are hosts.** Their `item.@this`/`ICreate` is the bridge (the goal Reader's own note) and goes. Plang carries them as `clr(goal)` / `clr<action>` (`Data<clr<goal>>`, `list<clr<action>>`); **C# properties stay plain** (`List<action>` — hosts hold hosts, `clr<>` only at the plang boundary).
3. **Hosts read/write through the `*` (reflection) kind, format-agnostic.** `Output` already does it (`Tagged.PropertiesFor`, View.Store → `[Store]`). `Read` and `Set` are its missing mirrors. The host's **C# class declaration is the single source of type knowledge** — the kind reflects `PropertyType` at the leaf. `JsonSerializer.Deserialize<goal>` and `GoalReadOptions` die (the goal ITypeReader ignored its format-agnostic reader and hardcoded STJ — the cheat).
4. **One construction door for items: `Create` — SYNC.** Dispatch is `T.Create(await Value(), this)`: the await sits **in front of** the door; `Create` receives a materialized item and works in memory (number parses its string, dict copies children). No async signature change anywhere. **No generic record builder** — every item writes its own `Create` (`permission` already does; a future record item writes its own too).
5. **kind owns cross-kind transforms** — `data.Convert(k)` → `kind.behavior.Convert` (**already async** — I/O-ish transforms live here): the html kind knows md→html; a kind that's just "build my type" (mp3→audio) delegates to the type's `Create`. `kind`/`strict` ride on `data.Type` — no extra params; strict enforced by timing (eager in `Create`, lazy at the value's load seam).
6. **Runtime `Create` door = a delegate on each `type.@this` entity.** One shared logic-free thunk on `type.@this` (`Builder<T>() => (v,d) => T.Create(v,d)`), closed **lazily per entity** on first use (`MakeGenericMethod` — the single reflective touch; a generator-emitted table is the later optimization). Users: the `as <type>` clause, kind→`Create` delegation, settings property binding. No static helper class; the entity owns closing its own builder.
7. **Registry = `list<type>`** (`app.type.list`), keyed name→entity index **on the collection**; `catalog` deleted. **Module discovery = `list<module>`** — views over the handler classes, reflection at the leaf, Fluid render for the LLM.
8. **Golden rule: the pattern never diverges.** Kind-varying construction lives on the kind — number's precision switches dissolve **in Stage 2, same move as the relocation** (no two patterns coexisting). Writes always go through the value's own kind (`Kind.Set`) — `SetValueOnObject` (8 arms from outside) is the write-side obpv and dies.
9. **`type.Build` dies — `FromRaw → type.Create`.** The defer rule is the entity door's first branch: wire-raw → lazy source (parse on first touch); already-a-value → the type's `Create`.

---

## Stage 0 — baseline + `[Obsolete]` marking

- Run the C# suite, **record the red count** (the baseline every later stage is measured against — no surprises, no rebuild-from-base to find out).
- Write the **unit test pinning the build error** (the clr(json)→`Actions` write throw) — red now, green after Stage 1.
- Mark the full removal list `[Obsolete]` (`convert.OfStatic`/`Of`/`Invoke`/`Discover`, `TryConvert`, `type.Convert(value)`, `type.Build`, `SetValueOnObject`, the goal/actions ITypeReaders, `GoalReadOptions`, `Describe()`/`StepActions`/`BuildTypeEntries`, `goal.getTypes`, `type.catalog.@this`, `catalog/view`, `item.OutputTagged`, number's switch family). If a project treats warnings as errors, coder picks the suppression story.

## Stage 1 — `pr-graph-hosts`

**Goal: goal/step/action/actions are plain C# hosts — one way to read/write/navigate a host.** (The builder's write blocker and the `%plan.steps%` failure fall out of this stage.)

- Drop `item.@this` + `ICreate` from the four classes.
- **Kind redesign — the kind IS the behavior** (settled 2026-07-09; `architect/stage1-kind-redesign.md`): the token/`behavior` split dies (anemic object + shadow behavior tree + per-verb registry hop). `kind.@this` = base class owning the verb defaults; each kind a subclass at **`type/<owner>/kind/<k>/this.cs`** (the `Type[t].Kind[k]` ruling applied: `type/item/kind/{json,list,dict,reflection}`; number's precisions land at `type/number/kind/<k>` in Stage 2); unknown kind = a base instance carrying the name. **One selection door:** `ctx.App.Type.Kind[name|clrType]` (collection = selection + lifecycle, never null; `Kinds`→`Kind` rename); the string implicit and `kind.Of` die (a static factory doing the collection's job); a kind building same-kind children passes `this`. Solves the list-kind name collision structurally (`sequence` rejected — every kind folder's class is `@this`, so the word `list` is usable).
- **Navigation re-derives the kind per hop** (settled 2026-07-09; `architect/stage1-navigation-answer.md`): the base walk asks **each node's own kind** for one `Descend` (renamed from `Step` — domain collision), re-deriving after every hop; the final node's kind builds the child Data. `*` descends **properties only**; the **list/dict kinds claim raw `IList`/`IDictionary`** (ClrForm assignable) and own index/key descent — no type-switch inside any kind (the bolted `is IList` branches were the `CoerceToKind` shape). json is untouched — homogeneous graphs re-derive to the same kind every hop; a future jsonpath walk stays available as a full `Navigate` override. "One kind walks the whole path" is only valid when the path is homogeneous — that was the broken contract.
- **`*`-kind `Set`** (mirror of its `Navigate`): reflect the property, then **one line — `value.Clr(PropertyType)`**, the value lowers itself; no if-chain, no carrier-opening at the call site. The dispatch lives inside: **`clr.Clr(target)` stops terminal-throwing and delegates to its kind (`Kind.Clr`, new kind verb)** — the json kind materializes its content into the CLR shape via the `Read` machinery. This IS the original blocker's fix, by its own error message ("the type must own this Clr projection" — it now does). `set %goal.Steps[i].Actions% = %compileResult.actions%`: `PropertyType = List<action>` + clr(json) → json kind builds the action hosts. No STJ stepping stone.
- **`*`-kind `Read`** — mirror of `Output` in *shape*, a **deserializer in scope** (coder review v3 #2): it re-derives what `Deserialize<goal>` did, by reflection, format-agnostic — instantiate the host, populate `[Store]` props by declared type, honor defaults/nulls/element types, recurse nested hosts (`Modifiers`, `actions`), and route `List<Data>` props (`action.Parameters`) through the **data reader's `@schema:data` path** (`%var%`-born/template/signing byte-identical). Serves both the `.pr` load and the `Set` conversion above — one mechanism. Budget it as the largest new surface of this stage.
- **`Read`'s source = the format-agnostic `IReader`** (settled 2026-07-09; `architect/stage1-read-source-answer.md`): `.pr` load is bytes → `json.Reader` → `Read(goal)` — **zero DOM**; `json.Clr` bridges its own element→reader (element→reader is json's format knowledge) and calls the same `Read`. Forward-only loop (`NextName`, wire order drives, `dict.Reader` shape). `List<Data>` props hand the *same positioned ref reader* to the data reader — no new JsonElement door, the existing byte entry, sign-identical for free. Not `Read(JsonElement)` — that re-bakes json into the host path (the goal-reader cheat, one level up) and diverges from "every reader reads through `IReader`."
- **DoD (widened, coder review v3 #1): full goal-graph round-trip, not one param.** Read the same `.pr` via STJ and via the reflection path *while both exist*, assert structural equality across the whole goal→steps→actions→params/modifiers graph (or pin a golden snapshot before STJ dies). The failure mode this guards is **silent field drift** — the one class of bug this stage can introduce. Plus the param **sign-identical** check (signature included).
- **data/reader routing** — `data/reader/this.cs:79-80`. Rule: String token → unchanged (text/plain; a full-match `%var%` keeps borning a `variable` in the born rule — untouched); non-String token **or** json-kind-declared → json → clr(json). Fixes `%plan.steps%` char-iteration.
- **Delete:** the goal/actions ITypeReaders (`goal/serializer/Reader.cs` + `Default.cs`, `actions/serializer/Reader.cs`), `Deserialize<goal>`, `GoalReadOptions` (`catalog/Conversion.cs:55-59`) + the goal dispatch (`:282`), the `.pr → goal-plang-type` MIME mapping.
- **Output consolidates — the loops are NOT identical** (coder caught my false "verified identical"; settled Option A, `architect/stage1-output-consolidation-answer.md`). Both loop `Tagged.PropertiesFor`, but diverge on **wire name** (`OutputTagged` honors `WireName`/`[JsonPropertyName]` → `"action"`; `reflection.Output` lowercases → `"actionname"` — a latent bug, `WireName`'s contract is STJ round-trip), **null-omission**, and the **value-write path**. Consolidation = **move `OutputTagged`'s body (WireName + WhenWritingNull + `WriteReflected`) onto `reflection.Output`**; hosts drop their overrides; `item.OutputTagged` deleted. Foreign-POCO casing changes lowercase→camelCase — accepted (no-backward-compat; camelCase is the canonical wire convention). Extend the round-trip DoD to a written-then-Read `.pr`.
- `Data<goal>` → `Data<clr<goal>>` (`goal/getTypes.cs:34`), `Data<action>` → `Data<clr<action>>` (`environment/run.cs:15`); `action.AsData` wraps `clr<action>`. Precedent: `clr<app>` (goalsSave).
- **Bridge-item audit — RESOLVED (settled with Ingi 2026-07-09), apply in this stage:**
  - **`GoalCall` = plang value.** It's in the `.pr` (`type:{name:"goal.call"}`; the data reader eager-reads it). Keeps `ICreate`; its `Convert` hook relocates to `GoalCall.Create` in Stage 2 (that worklist line stays).
  - **`snapshot` = DEFERRED — not converted in this branch** (settled with Ingi after the coder's check failed; supersedes the earlier "host" call). Today's snapshot is a pre-model implementation (Section bag + a `Navigate` override digging into `Section("Variables")` — no real properties for reflection to walk), so converting it now forces variable-knowledge *into* snapshot (the rejected `snapshot/variables` reify — wrong owner). The real fix is its own branch: **`ISnapshot`** — each app property snapshots/restores *itself*; snapshot becomes a dumb serializable container (see `Documentation/v0.2/todos.md` 2026-07-09). In this branch snapshot stays item-with-override, a **marked exception to item⟺ICreate** (known debt, exit path named); the only touch is Stage 2: when `SetValueOnObject` dies, its snapshot arm reroutes to snapshot's **existing** `SetVariable` door as its own child-write. No new class, no reify.
  - **`app` = host.** The live singleton; `clr<app>` (goalsSave) is already the precedent. Drops item+ICreate here.
  - **`catalog/view` = obsolete.** The schema view over the catalog — Stage 4's Fluid render replaces its job; dies with catalog. Add to the Stage-0 `[Obsolete]` list.

Note: `SetValueOnObject` only *shrinks* here (the clr arm covers host writes); full deletion is Stage 2.

## Stage 2 — `create-unification` (items only)

**Goal: one construction door; the reflective hub dies.**

- **Relocate** the per-type static `Convert` hooks → each type's own **sync** `Create` (number, text, datetime, date, time, duration, bool, binary, guid, image, path, dict, list; GoalCall per the Stage-1 audit). What changes: the door (hub→`Create`), the kind source (param→`data.Type.Kind`), the return convention (`context.Ok/Error`→`return`/`data.Fail`). **Sync is safe by construction** (coder review v3 #4): every hook is a sync static today — the reflective `Discover` *requires* that signature — so none can await; confirm per relocation, and if one ever surprises it routes to the async `kind.behavior.Convert` door instead of breaking sync `Create`.
- **number's kinds dissolve HERE, in the same move** (not a follow-on — no two patterns coexisting, per the golden rule): `number.Create` is a thin dispatcher; each precision owns its build at `type[number].kind[<k>]`; the switch family (`CoerceToKind` + serializer read/write switches + `FromDoubleAsKind`) dissolves. **Kind = storage type** (declaration → source → default `long`, a setting). **Precision = decimal places, edge-only** (max in every calculation; round at output or explicit request). Overflow/mix policy (`Overflow.Promote`/`Precision.Error`) settings-carried — stays.
- **`type.Build` dies — the flow becomes `FromRaw → type.Create`** (settled). The defer rule is the entity door's **first branch**: wire-raw (string/bytes, not yet a value) → lazy source, parse on first touch — laziness and verbatim passthrough intact; already-a-value → the type's `Create`; a variable name → the variable type's own `Create` (Resolve). Build's construction arms were the second way of doing what `Create` does; the whole method goes.
- **`ICreate` default shrinks** to: pass-through → facet → decline. (Its `convert.OfStatic` tail, `type.Create(raw)` lift, and dict/list `Clr(typeof(TSelf))` branch all go.)
- **Entity `Create` delegate** on `type.@this` (model #6). `OwnerOf`/`_ownership` survives as the private index behind `type.Create(raw)` and the delegate bind — not a public door.
- **Delete:** `convert.OfStatic`/`Of`/`Invoke`/`Discover`; `type.Convert(value)` (`type/this.cs:187`); `TryConvert`'s construction stages (`catalog/Conversion.cs:128-522` — primitive `ChangeType` lowering stays in `item.Clr`; verify callers `type/this.cs:602`, `setting/this.cs:102` route through `Create` first); **`SetValueOnObject`, the whole method** — every write navigates to the target and calls its kind's `Set`. (The snapshot arm reroutes to snapshot's existing `SetVariable` as its own child-write — snapshot is the deferred exception, see the audit.)
- **`data.Convert(kind)` becomes real:** `kind.behavior.Convert` gains converters; a kind whose transform is "build my type" delegates to the entity `Create`.
- **`goal.getTypes` is `[Obsolete]` — it goes away** (settled with Ingi; corrects my earlier "own line, own test" fold of coder review v3 #5). It's a string-typed shadow of the type system: `dict<string,string>` of type *names*, a regex parsing `"list<goal>"` for the element type, `GetTypeNameStatic` over reflected `Run()` returns — every piece now has a model owner (`app.type["name"]` entities, the entity answering its element, the Stage-4 action view answering return types). Its List-lower crash *is* the same `ClrConvert` terminal throw as the blocker (verified, but the target is a plang wrapper — a convert-via-LOWER mis-route); **keep-alive only**: the Create rerouting this stage does incidentally un-crashes it so the builder can proceed. No dedicated investment. Dies at Stage 4, where the compile prompt gets per-step scope types from the views/type entities (the replacement is shaped there — type *entities*, never name strings).

## Stage 3 — `catalog-removal`

- **`app.type.list` = `list<type>`** + keyed name→entity index on the collection. **Runtime-hot** (`[name]→entity` is hit on every `.pr` read) — land and test apart from the reparent tail. Population stays lazy + runtime-extendable (module choice types, `code.load`); the `list`/`type` self-reference is data, harmless.
- Rehome `Kinds`/`Readers` to `app.type.*`; mechanical tail: `Renderers`/`KindHooks`/`Compares`/`Scheme`/`Choices` reparent (zero logic — the release valve, may trail as its own commits).
- Delete `type.catalog.@this`.

## Stage 4 — `module-discovery`

- `app.module.list : list<module>`; `module.Actions : list<action>` (the module-tree view — same action, class-level zoom); `action.Properties : list<type>` keyed by property name. **Views over the handler classes** — reflection once, at the `action` view leaf (unwrap `Data<T>`/`[Code]T` → plang type); consumers read `type.Name`, never a `System.Type`, never `GetTypeName` at a call site.
- **What replaces `Describe()` (settled):** Fluid `.md` templates under `os/system/builder/templates/` (e.g. `modules.md`) — part of the builder app, user-editable. Rendered by the builder's own goals: `get all modules → %modules%`, `ui.render 'templates/modules.md' → %doc%`. **Structure** comes from the `list<module>` views; **prose** from the markdown that already exists at `os/system/modules/<module>/*.{description,notes,examples}.md` — the template weaves both. Drill-in: `filter %modules% where action=… → render`.
- **Delete** `Describe()`, `StepActions`, `BuildTypeEntries(modules)`. Check teaching parity (examples/defaults/return-types currently folded in `Describe()`).

## Stage 5 — the removal sweep

- Scan for every remaining `[Obsolete]` (Stage 0's marks). Delete what can die; anything that genuinely can't yet stays marked, with one line in this plan's close-out saying why — known future debt, findable by attribute scan.
- Success measure: the scan count, before vs after.

---

## Stays — do NOT remove

- `app/data/reader/this.cs` — only the `:79-80` routing line changes (Stage 1). `%var%`/template/signing byte-identical throughout.
- `item.Clr`/`ClrConvert` — the plang→CLR lower exit (a different direction). `dict.Clr`'s STJ for genuine map lowering; `list.Clr`.
- The **deferral rule** (wire-raw → lazy source; variable name → variable) — survives as the *first branch of the entity `type.Create`* (Stage 2). `type.Build` the method dies.
- `type.Convert(string)`/`FromWire` — wire reconstruction (snapshot/crypto); verify callers in Stage 2, likely stays.
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
