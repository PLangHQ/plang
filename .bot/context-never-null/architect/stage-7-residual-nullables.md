# Stage 7: Residual actor/context nullables (the A–D sweep missed these)

**Goal:** Close the context/actor nullables that Stages 1–6 did not enumerate, so the Stage 6 invariant proof actually holds end to end. A later nullable-object audit (the `duplicate-execution-paths` scan) found four pockets outside mechanisms A–D: the per-actor **channel Actor fields**, the `context.CallStack` read-through getter the plan deferred, a nuance in the `type`-entity flip, and the residual `!` on members that are already non-null.

**Scope:**
- **Included:** `channel.@this.Actor` / `.Channels` and `channel.list.@this.Actor` (Actor fields mechanism A never listed); `context.CallStack` (demolition.md deferred it "audit case by case" — resolved here); the `type`-entity identity-mint sites (refines mechanism B's one-line "`type.Context` non-null"); the `Context.App!` / `ctx.Context!` bang residue.
- **Excluded:** the seven value-type contexts (`type`, `dict`, `list`, `path`, `clr`, `computed`, `source`) and `Error.Context` — **already owned by mechanism B / Stage 3; do not re-do them here.** Everything in Stages 1–5.

**Deliverables:**

1. **Channel actor fields → genuinely non-null (decided — service is an actor).** `channel.@this.Actor` (`channel/this.cs:72`), `channel.@this.Channels` (`:80`), and `channel.list.@this.Actor` (`list/this.cs:35`) are declared `= null!` today, with a `_app.System.Context` fallback for "service-owned channels have no Actor" (`list/this.cs:42`: `Actor?.Context ?? _app.System.Context`). That premise is wrong: **Service is an actor.** The App is born with **2 actors (system, user) and 3 channels**; the `service` actor is **transient** — created on demand (e.g. an HTTP response that needs its own execution/warning/error context) and disposed when done. A service-owned channel carries that transient service actor for its whole life, so `Actor` and `Channels` are never null. The `null!` was the inverse of the rest of the branch — a non-null declaration hiding a real null — so consumers defensively `?.` it anyway (`channel/this.cs:185,196,248,250,296,301,314`; `list/this.cs:42,137`).
   - Flip all three to genuinely non-null. Delete the `_app.System.Context` fallback (`list/this.cs:42`), every `Actor?.` / `Channels?.App` / `Actor?.Context` defensive site, the "Service-owned Channels have no Actor" comments, and the "firing with no Actor — handlers receive null context" debug path (`channel/this.cs:250`). The service actor is created+disposed as a unit with its channel(s), so there is no live channel with a dead actor.
   - **Deferred (Ingi, "for later" — not this stage):** `channel.@this.Channels` (the back-reference to the parent collection) should be named for its type — `channel.list`, not `Channels`. Naming only; the non-null flip above stands regardless.

2. **`context.CallStack` → non-null** (`actor/context/this.cs:49`). `App` is already non-null (`:38`) and `App.CallStack` is `= new()` (`app/this.cs:283`), so `App?.CallStack` flips to `App.CallStack` and the property type drops its `?`. Consumers reading the stack drop the `?.` on CallStack itself (`error/Error.cs:179`, `module/debug/tag.cs:35`) but **keep `?.Current`** — `Current` is genuinely null at rest, between calls. This resolves the case the plan deferred (demolition.md stays-list line 100 / additions line 114).

3. **`type`-entity identity mints — refine mechanism B.** Stage 3 / demolition.md line 48 flips `type.Context` to non-null. The nuance to record: the **static primitive table stays** — `StampPrimitive` (`type/this.cs:128`) seeds `_clrType`, and `ClrType`'s `?? AppTypes.GetPrimitiveOrMime(Name)` fallback (`:149`) answers the CLR mate of a primitive without an App. That answer is global (the CLR type of "text" doesn't vary per actor) and is **independent** of whether the `Context` field is nullable. Do not delete it thinking it's the nullable crutch. To make the flip clean, thread the in-scope context into the identity-only mint sites: `FromName("list"/"dict")` at `module/list/where.cs:42,50` and `range.cs:35` (these sit literally inside `Context.Ok(...)` — the context is in hand, just not passed), `new type("image")` at `type/image/this.cs:93`, and `new type("file")` at `type/file/this.cs:53`.

4. **Residual `!` sweep — acceptance cleanup.** Once Stages 1–4 land, the null-forgiving `!` on already-non-null members is dead noise: `Context.App!` (16 sites) and `ctx.Context!` (6 sites — `ReadContext.Context` is already non-null). Remove them. The two counts reaching zero is a clean completion check for the branch.

**Dependencies:** Items 2–4 sit on Stage 1 (App/Context non-null) and Stage 3 (`type.Context` flip). Item 1 is independent of Stages 1–6 and now decided, so it can land any time (it pairs naturally with Stage 1's actor work). Natural sequence: after Stage 6, since this stage closes gaps the proof assumed away.

## Design

The A–D mechanisms swept the fields that *hold* a context as state and the construction windows around them. They missed three shapes that aren't plain state fields:

- **A `null!` that lies the other way (item 1).** Mechanism A flips `?` that is never null. The channel Actor fields are the mirror image — declared non-null, actually null for service channels — so they were invisible to a `?`-hunt. They carry the same tax (every consumer `?.`s them) and the same cure (make the declaration honest). Decided: Service *is* an actor (transient — born for an HTTP response that needs its own execution/warning/error context, disposed when done), so service channels carry it and the fallbacks vanish. The "App is born with 2 actors + 3 channels" floor means the only actor-less collection the old fallback served — service — no longer exists.

- **A read-through getter (item 2).** `context.CallStack` isn't a state field; it forwards `App.CallStack`. The plan correctly said "don't blanket-flip getters" — but this one's upstream (`App`, and `App.CallStack`) is now provably non-null, so the audit lands on "flip it." `?.Current` stays because *that* upstream really is absent between calls.

- **A capability vs a field (item 3).** The `type` entity conflates two things the flip must keep separate: the **field** `Context` (flips to non-null, mechanism B) and the **static identity answer** (`GetPrimitiveOrMime`, stays). They're orthogonal; the static table is real engine knowledge, not a nullable workaround.

No new verb+noun surface and no decomposition: item 1 reuses the existing `service` actor whole, item 2 is a getter body, items 3–4 are deletions. OBP-clean.

Cross-reference: `plan.md` mechanisms A (Actor fields) and B (`type.Context`); `plan/demolition.md` sections A/B and the stays-list. This stage extends A's Actor enumeration and refines B's `type` row.

## You own this

The coder owns shapes, site lists, and sequencing — including how the transient `service` actor is constructed and disposed around an HTTP response. The design is settled: Service is an actor, so the channel Actor fields are genuinely non-null (not a stays-list null). The contract: after this stage, `channel.Actor` / `channel.Channels` / `channel.list.Actor` are non-null with no `Actor?.` / `Channels?.App` fallbacks, `context.CallStack` is non-null, the `type` static identity answer survives the `Context` flip, and no `Context.App!` / `ctx.Context!` remains. The `channel.Channels` → `channel.list` rename is deferred (Ingi) and not part of this stage.
