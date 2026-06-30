# Stage 7: Residual actor/context nullables (the A–D sweep missed these)

**Goal:** Close the context/actor nullables that Stages 1–6 did not enumerate, so the Stage 6 invariant proof actually holds end to end. A later nullable-object audit (the `duplicate-execution-paths` scan) found four pockets outside mechanisms A–D: the per-actor **channel Actor fields**, the `context.CallStack` read-through getter the plan deferred, a nuance in the `type`-entity flip, and the residual `!` on members that are already non-null.

**Scope:**
- **Included:** `channel.@this.Actor` / `.Channels` and `channel.list.@this.Actor` (Actor fields mechanism A never listed); `context.CallStack` (demolition.md deferred it "audit case by case" — resolved here); the `type`-entity identity-mint sites (refines mechanism B's one-line "`type.Context` non-null"); the `Context.App!` / `ctx.Context!` bang residue.
- **Excluded:** the seven value-type contexts (`type`, `dict`, `list`, `path`, `clr`, `computed`, `source`) and `Error.Context` — **already owned by mechanism B / Stage 3; do not re-do them here.** Everything in Stages 1–5.

**Deliverables:**

1. **Channel actor fields — DECISION REQUIRED (Ingi), then flip.** `channel.@this.Actor` (`channel/this.cs:72`), `channel.@this.Channels` (`:80`), and `channel.list.@this.Actor` (`list/this.cs:35`) are declared `= null!` — non-null on paper — while null **genuinely occurs** for service-owned channels (`list/this.cs:42`: `Actor?.Context ?? _app.System.Context`). This is the inverse of the rest of the branch: instead of a `?` that's never null, it's a `null!` that *is* sometimes null, so consumers defensively `?.` it anyway (`channel/this.cs:185,196,248,250,296,301,314`; `list/this.cs:42,137`). The tree contradicts itself on the root question: `app/this.cs:237` documents a **"Service-as-actor model"** and `GetActor`'s set is system/user/**service**, yet the channel comments insist "Service is not an Actor."
   - **Option 1 (recommended — matches the invariant and the Service-as-actor model):** service-owned channels belong to the `service` actor. `Actor` / `Channels` become genuinely non-null; delete every `Actor?.` / `Channels?.App` / `Actor?.Context` fallback and the "firing with no Actor — handlers receive null context" debug path (`channel/this.cs:250`).
   - **Option 2:** Service is genuinely not an actor → declare `Actor?` honestly (drop the `null!` lie) and add it to the stays-list as a real null.
   - Either resolution removes the dishonest `null!`. The decision is which way: it settles "is Service an actor," which mechanism A left open by not enumerating these fields.

2. **`context.CallStack` → non-null** (`actor/context/this.cs:49`). `App` is already non-null (`:38`) and `App.CallStack` is `= new()` (`app/this.cs:283`), so `App?.CallStack` flips to `App.CallStack` and the property type drops its `?`. Consumers reading the stack drop the `?.` on CallStack itself (`error/Error.cs:179`, `module/debug/tag.cs:35`) but **keep `?.Current`** — `Current` is genuinely null at rest, between calls. This resolves the case the plan deferred (demolition.md stays-list line 100 / additions line 114).

3. **`type`-entity identity mints — refine mechanism B.** Stage 3 / demolition.md line 48 flips `type.Context` to non-null. The nuance to record: the **static primitive table stays** — `StampPrimitive` (`type/this.cs:128`) seeds `_clrType`, and `ClrType`'s `?? AppTypes.GetPrimitiveOrMime(Name)` fallback (`:149`) answers the CLR mate of a primitive without an App. That answer is global (the CLR type of "text" doesn't vary per actor) and is **independent** of whether the `Context` field is nullable. Do not delete it thinking it's the nullable crutch. To make the flip clean, thread the in-scope context into the identity-only mint sites: `FromName("list"/"dict")` at `module/list/where.cs:42,50` and `range.cs:35` (these sit literally inside `Context.Ok(...)` — the context is in hand, just not passed), `new type("image")` at `type/image/this.cs:93`, and `new type("file")` at `type/file/this.cs:53`.

4. **Residual `!` sweep — acceptance cleanup.** Once Stages 1–4 land, the null-forgiving `!` on already-non-null members is dead noise: `Context.App!` (16 sites) and `ctx.Context!` (6 sites — `ReadContext.Context` is already non-null). Remove them. The two counts reaching zero is a clean completion check for the branch.

**Dependencies:** Items 2–4 sit on Stage 1 (App/Context non-null) and Stage 3 (`type.Context` flip). Item 1 is independent of Stages 1–6 but needs Ingi's decision before coding. Natural sequence: after Stage 6, since this stage closes gaps the proof assumed away — but settle item 1's decision whenever it's convenient, it doesn't block 2–4.

## Design

The A–D mechanisms swept the fields that *hold* a context as state and the construction windows around them. They missed three shapes that aren't plain state fields:

- **A `null!` that lies the other way (item 1).** Mechanism A flips `?` that is never null. The channel Actor fields are the mirror image — declared non-null, actually null for service channels — so they were invisible to a `?`-hunt. They carry the same tax (every consumer `?.`s them) and the same cure (make the declaration honest). The recommended cure aligns with the branch: if Service is an actor (the model `app/this.cs:237` already names), service channels carry it and the fallbacks vanish.

- **A read-through getter (item 2).** `context.CallStack` isn't a state field; it forwards `App.CallStack`. The plan correctly said "don't blanket-flip getters" — but this one's upstream (`App`, and `App.CallStack`) is now provably non-null, so the audit lands on "flip it." `?.Current` stays because *that* upstream really is absent between calls.

- **A capability vs a field (item 3).** The `type` entity conflates two things the flip must keep separate: the **field** `Context` (flips to non-null, mechanism B) and the **static identity answer** (`GetPrimitiveOrMime`, stays). They're orthogonal; the static table is real engine knowledge, not a nullable workaround.

No new verb+noun surface and no decomposition: item 1 reuses the existing `service` actor whole, item 2 is a getter body, items 3–4 are deletions. OBP-clean.

Cross-reference: `plan.md` mechanisms A (Actor fields) and B (`type.Context`); `plan/demolition.md` sections A/B and the stays-list. This stage extends A's Actor enumeration and refines B's `type` row.

## You own this

The coder owns shapes, site lists, and sequencing. The one thing that is **not** the coder's to settle is **item 1's Option 1 vs Option 2** — that's Ingi's call on whether Service is an actor. Everything else (the CallStack getter body, which mint sites to thread, the order of the `!` sweep) is yours. The contract: after this stage, `context.CallStack` is non-null, the `type` static identity answer survives the `Context` flip, no `Context.App!` / `ctx.Context!` remains, and the channel Actor fields are honest in whichever direction Ingi picks.
