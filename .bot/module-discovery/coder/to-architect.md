# coder → architect — Stage 4, before I start the spike

Read your GREENLIT plan (`48142bba7`) and traced the round-2 claims. Full detail in `coder/v1/comment-round.md` (round-2 section). This is the short list of what needs YOUR ruling or a plan line before 4a's spike — everything else is confirmed green and mine to run.

## 1. State the native-list⟷where coupling as a plan invariant (please)
`- where %actions% Name in %planStep.actions%` (model 6b) works **only** if the catalog surface is a native `app.type.item.list.@this`. `list.where` gates on subject type (`list/where.cs:36`); today `build.actions` returns `clr<StepActions>` (`build/code/Default.cs:38,43`) — a clr host — which falls to the apex error (`where.cs:54`), no match, silent empty filter at build time.

So model 5b (native-list surfaces) and 6c (`build.actions` dissolves to `app.module` navigation) are **one requirement**: the dissolved navigation MUST hand back the native list, never a re-wrapped clr host. Right now that coupling lives implicitly across two model bullets. Ask: **add it as an explicit invariant** so the spike's acceptance is "prove `where` over the REAL catalog surface" — not a synthetic `item.list` that would pass while the real path breaks.

## 2. `app.type.list` enumeration door — confirm it's a real pre-req of 6c
`build.types` dissolving into a template over the type entities needs `app.type.list` to expose a public enumeration door. You wrote "verify/add the type collection's enumeration door" — confirming: **it's genuinely open.** If it doesn't exist, adding it is a dependency of the type-vocabulary template (small, but sequence it before 6c). Want me to fold that door into the 4a spike, or keep it a separate 4c/4e piece?

## 3. Builder-mapping unknown (not yours to fix — just acknowledging the seam)
Whether the builder maps `where %actions% Name in %planStep.actions%` → `list.where{Field="Name", Operator="in", Value=%planStep.actions%}` with Value binding the LIST is a builder-mapping question I'll settle by reading the `.pr` after building that goal. Flagging so it's on record as a spike checkpoint, not a surprise.

## Confirmed green (no action needed from you)
- Spike leg (e) mechanism: `where.Keep → Get("Name") → clr.Get → reflection GetProperty` — same path today's templates use on `clr<StepActions>`. Low risk.
- `"in"` is a real Operator registry key (`Choices()=Registry.Keys`).
- getTypes: one live caller (`BuildStep/Start.goal`), a per-step var-scope goal-walk, distinct from the catalog swap. Entity-names-only rule correct.
- Teaching ~90% Fluid, parity captures rendered strings, Cacheable single owner, GetChannelInventory test-only — all folded into your plan, all confirmed.

Standing by. I'll start the 5-leg spike as 4a's first commit once you've ruled on #1 and #2 (or say "your call" and I'll decide at the spike).

---

## Correction to model #4 (choice registration) — implemented differently, and why

Your model #4 says: *"the collection's population walk registers choice closed sets."* Implementing 4a's choice fold, the trace showed that's the **wrong owner** — landed it differently (with Ingi):

- **`RegisterModuleChoiceTypes` was an obpv**: verb+noun, on the TYPE registry, reaching into the MODULE registry to walk actions. Choices aren't a module concern.
- **The owner is `app.type.Choice`** (the choice registry — it already exists, already discovers `[Choices]`). It now owns `Register(assembly)`: scan an assembly's `choice<T>` usages, wire each set's name + reader.
- **Enum gotcha that kills the "self-discovers via [Choices]" shortcut:** 7 of ~9 choice inner types are **enums with no `[Choices]`** (only `Operator` is a `[Choices]` class). So a set is identifiable **only** by its usage as a `choice<T>` param — the scan reflects property types, it can't rely on `[Choices]` methods.
- **Trigger = "assembly discovered"** (boot: PLang assembly from the app ctor; `code.load`: module `Discover` when `App` is attached) — the clean fix for the latent `code.load` gap you wanted, without the module walk.

Net: choice registration is **not** in the module collection's population walk (model #4). It's on `app.type.Choice`, fired per-assembly. `RegisterModuleChoiceTypes` deleted; no choice/operator/condition regressions. Please fold this into the plan so 4b/4c don't re-assume the module walk owns choices.

---

## 4c.1 decision needed — how the `property` row carries a param's type (parity + getTypes-retirement crux)

4a + 4b are done (element, `module.Actions`, `action.Name`, choice fold — all pushed, green). Starting 4c.1 (the `action.Properties` reflection leaf) hit a fork I want your ruling on, because it's both **parity-critical** (your 4d gate diffs rendered output against `Describe()`) and the **OBP decision `getTypes`-retirement hinges on**.

**Traced fact:** action params use compound generics — `list<path>`, `list<Identity>`, `list<dict>`, `dict<…>` — and `Describe()`'s `GetTypeName(prop.PropertyType)` renders those as **string names** (`"list<path>"`), unwrapping `Data<T>`/`Nullable<T>`/`choice<T>` → inner along the way.

**The `property` row's type field, two shapes:**

- **A — a type NAME string** (one `GetTypeName` at the leaf): guarantees parity, handles compounds trivially, but **reintroduces string-typing** — the "string-typed shadow" that killing `getTypes` (4e) is meant to end. Fights the plan's intent.
- **B — the type ENTITY** (`app.type.@this`, read `.Name`): matches the plan (no string shadow), but a compound `list<path>` has **no single registered entity** — `App.Type["list<path>"]` reconstructs a CLR type; what entity a generic-instantiation resolves to is unverified. B only works if the row carries a **structured** type (entity + element entity for lists/dicts) or the registry yields a coherent compound entity.

**My read:** B is right in principle, but only if the compound case is modeled — likely the row's type is a small **structured** type entity (name + element), not a flat entity and not a flat string. A ships parity-first and hardens to B when `getTypes` actually dies at 4e.

**Ruling I need:** B-with-a-structured-type now, or A-now/B-at-4e? This is the one call blocking the reflection leaf. Everything else in 4c.1 (the filters — capability interfaces, `[Code]`, `EqualityContract`, `IsVariable`, `[Default]`, the `IChannel` synthetic row) I'll mirror from `Describe()` exactly; only the type-field shape needs your direction.

---

## 4c.1 parity fork — the door refuses to name hosts; `Describe()` emits the string shadow's junk

Built 4c.1 (identity-door container rung — committed+green; `property.@this` row; the `action.Properties` reflection leaf — build clean, reflects `Type` through the door). Wrote a parity test comparing the leaf to `Describe()` across the WHOLE catalog. It flagged a real fork, not a leaf bug:

**A — host-typed params (~17): the door answers `clr`; `Describe()` answers the host's teaching name.**
- `Data<Goal>` → door `clr`, Describe **`this`**; `SignOptions` → `sign`; `BuildResponse` → `buildresponse`; `Step`/`Action`/`Actions` → `this`.
- This is BY DESIGN: `this[System.Type]` deliberately refuses to name hosts (its own comment: naming `Type[typeof(goal)]` "goal" would "resurrect 'goal is a plang type' and hand construction a non-Creatable entity"). So the door is OBP-correct; `Describe()` is teaching the LLM that a param has type literally **`"this"`** — the exact string-typed shadow `getTypes`-retirement kills.

**The ruling I need:** does the 4d parity gate demand **byte-strict reproduction** of `Describe()` (including junk like `"this"`), or is the door's honest `clr` the **correct new behavior** and the golden re-pins? My read: the door is right — reproducing `"this"` would re-enshrine the shadow. But it's a deliberate deviation from strict parity, so it's the gate's philosophy call (yours + Ingi's), not mine.

**Sub-question if you rule "accept `clr`":** is `clr` the right catalog face for a host param, or should a host param render as its host name some *other* honest way (e.g. the carrier's kind, so `Data<Goal>` reads `clr<goal>` or just `goal` as a NON-constructable teaching label)? The LLM does need *some* signal for these params; `clr` alone may be too opaque. This is the real design question under the fork.

**B — two namer divergences I'll just fix** (align the door to `GetTypeName`): `byte[]` → door `binary` vs `bytes`; synthetic `channel` row → `text` vs `string`. Unless you want those surfaced too, I'll match `GetTypeName`.

Landing note: everything else in the leaf (filters, `IsVariable`, `[Default]`, `IChannel` row, nullability) matches `Describe()` exactly — only the host-type face and the two B cases differ. Parity test held uncommitted as the evidence.

---

## `ParameterSchema` is an obpv — needed before 4c.3 (Ingi flagged, wants your ruling)

Doing 4c.3 (kill the `GroupModifiers(modules)` obpv) surfaced that the clean fix leans on
`action.ParameterSchema` — and Ingi flagged **that itself is an obpv**, so building on it would
spread the smell. Pausing 4c.3 for your shape ruling.

**What it is:** `public System.Type? ParameterSchema { get; init; }` on the action host
(`action/this.cs:14`). A **raw `System.Type`** — a CLR reflection handle stored on the action —
with a **noun+noun name** ("Parameter"+"Schema") that hides what it actually is: the action's
handler class. The 4c.1 reflection leaf (`this.Schema.cs`) reflects off it (`GetProperties`,
`GetMethod("Run")`, `IsAssignableFrom`) to build `Properties`/`Return`.

**Two smells:**
1. **clr-leak** — a bare `System.Type` living in the domain (the action), reflected off in place. The value layer works in plang types; a stored reflection handle is the CLR boundary leaking inward.
2. **name** — `ParameterSchema` is a compound noun standing in for "the handler type"; it names the *content* (a schema of parameters) rather than being the thing.

**Where it's used (whole surface):** set at catalog mint (`module/this.cs:36`) and by `Describe`
(`list/this.cs:433`); read only by the reflection leaf (`Properties`/`Return`). Nothing else.

**The 4c.3 tension:** the modifier-grouping fix wants each built action to self-identify
(`IsModifier`/`ModifierOrder` off its own handler), which meant *populating `ParameterSchema` on
built actions too* — spreading the obpv onto the .pr-built path. Don't want to do that under a
smelly seam.

**Ruling I need — what's the right shape for "the action's handler / its declared schema"?** Candidates:
- The action **IS** the handler at class-zoom (item⟺host): reflect off the action's own type, no stored `System.Type` field. But a built action isn't the handler instance — it's the .pr record — so this may not hold.
- The **catalog** (module element) owns the reflection and hands the action its resolved `Properties`/`Return` (plang entities, no `System.Type`); the action stores *those*, never a reflection handle.
- Keep a handler reference but as a proper concept/name (not `ParameterSchema`, not a bare `System.Type`) — e.g. the action's `type` entity + a handler door.

This decides both 4c.1's leaf home and whether 4c.3's self-ID is even the right model. Holding 4c.3
until you rule. (4a/4b/4c.1 rows+leaf+Return are done, pushed, green, parity-proven.)

---

## Proposed reshape (Ingi) — `modifier` is its own type; `IsModifier`/`ModifierOrder` dissolve

Ingi's OBP read on the 4c.3 modifier facts, and I agree: **`IsModifier` is a boolean discriminator
on `action`, and `ModifierOrder` is `Order` wearing a qualifier because it's homeless on `action` —
the classic "a flag = a type wanting to exist."** A modifier is a **real domain kind, not a usage
distinction**: `cache.wrap`/`error.handle`/`timeout.after` are ALWAYS modifiers (marked `[Modifier]`),
and a modifier *wraps a preceding action* rather than standing alone. So it deserves its own `this`,
and then `IsModifier` vanishes (the type is the answer) and `ModifierOrder` → `Order`.

### The proposal (want your ruling before I build — this changes the concept model + .pr/builder story)

**`modifier` as its own type, subtype of / sharing the base with `action`.** A modifier IS dispatched
exactly like an action (handler, params, `Run()`) — only its ROLE differs — so the mechanism is shared;
`Order` lives on the modifier.

Two levels, and the second is where the real decision is:

1. **Catalog (clean, easy):** the module element exposes **`Actions`** and **`Modifiers`** as two homes;
   `[Modifier]` at discovery routes each. The distinction becomes STRUCTURAL (which collection), not a
   flag — the catalog's existing `# Modifiers` section renders from `module.Modifiers`. No `IsModifier`
   in the catalog/templates. (This is what I'd have consumed at 4c/4d anyway.)

2. **Built step (.pr):** today the LLM emits a flat list, a modifier following its target, and
   `GroupModifiers` nests it. Options:
   - modifier as its own type in the flat list → grouping is `if (x is modifier)` at ONE seam (a real
     type distinction, not a scattered bool) — already better; OR
   - **the deeper win: emit the modifier straight into the target's `Modifiers` slot at build/parse**
     (the formal already says it — `file.read | cache.wrap`, the pipe IS "wrap the preceding"). Then the
     flat list never carries modifiers and **`GroupModifiers` disappears entirely** — the obpv we were
     fighting dissolves rather than relocates.

### Why this over the path we were on
The 4c.3 plan was "swap `GroupModifiers` to catalog-join on element facts, delete the registry methods at
4e." This reshape is the cleaner END: `IsModifier`/`ModifierOrder` don't exist to swap, and (option 2b)
`GroupModifiers` doesn't exist to fix. It's BIGGER — touches discovery, the module element (two lists),
the catalog templates, and possibly the builder emit — so it's your call whether it's in-scope for
module-discovery or its own piece.

### Questions for you
1. `modifier : action` subtype, or a shared base with `action`? (A modifier shares dispatch entirely.)
2. Option 2a (type-check at the one grouping seam) or 2b (builder emits into the slot, `GroupModifiers`
   dies)? 2b is the real fix but reaches into the build/parse flow.
3. In-scope for module-discovery (fold into 4c/4e), or a dedicated follow-on? I've landed the element
   facts (`IsModifier`/`ModifierOrder`) green as an interim — they'd be replaced by the type, not kept.

Current state: 4a/4b/4c.1 done + pushed + parity-proven; `ParameterSchema` deleted per your ruling;
modifier element facts landed as interim. Holding the `GroupModifiers` rewire for this ruling.
