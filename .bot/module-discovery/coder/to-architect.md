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
