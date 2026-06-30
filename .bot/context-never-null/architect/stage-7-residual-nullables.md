# Stage 7: Residual context nullables (the A–D sweep missed these)

**Goal:** Close the **context** nullables that Stages 1–6 did not enumerate, so the Stage 6 invariant proof holds end to end. A later nullable-object audit found three context pockets outside mechanisms A–D: the `context.CallStack` read-through getter the plan deferred, a nuance in the `type`-entity flip, and the residual `!` on members that are already non-null.

**Scope:**
- **Included:** `context.CallStack` (demolition.md deferred it "audit case by case" — resolved here); the `type`-entity identity-mint sites (refines mechanism B's one-line "`type.Context` non-null"); the `Context.App!` / `ctx.Context!` bang residue.
- **Excluded:** the seven value-type contexts (`type`, `dict`, `list`, `path`, `clr`, `computed`, `source`) and `Error.Context` — **already owned by mechanism B / Stage 3; do not re-do them here.** Everything in Stages 1–5.
- **Moved out:** the channel **Actor** fields (`channel.Actor` / `.Channels`, `channel.list.Actor`) are actor work, not context. They go with the other actor-side nullables on the `duplicate-execution-paths` branch (`.bot/duplicate-execution-paths/architect/nullable-objects.md`), keeping this branch focused on context. The decision is recorded there: Service is a transient actor, so those fields flip genuinely non-null.

**Deliverables:**

1. **`context.CallStack` → non-null** (`actor/context/this.cs:49`). `App` is already non-null (`:38`) and `App.CallStack` is `= new()` (`app/this.cs:283`), so `App?.CallStack` flips to `App.CallStack` and the property type drops its `?`. Consumers reading the stack drop the `?.` on CallStack itself (`error/Error.cs:179`, `module/debug/tag.cs:35`) but **keep `?.Current`** — `Current` is genuinely null at rest, between calls. This resolves the case the plan deferred (demolition.md stays-list line 100 / additions line 114).

2. **`type`-entity identity mints — refine mechanism B.** Stage 3 / demolition.md line 48 flips `type.Context` to non-null. The nuance to record: the **static primitive table stays** — `StampPrimitive` (`type/this.cs:128`) seeds `_clrType`, and `ClrType`'s `?? AppTypes.GetPrimitiveOrMime(Name)` fallback (`:149`) answers the CLR mate of a primitive without an App. That answer is global (the CLR type of "text" doesn't vary per actor) and is **independent** of whether the `Context` field is nullable. Do not delete it thinking it's the nullable crutch. To make the flip clean, thread the in-scope context into the identity-only mint sites: `FromName("list"/"dict")` at `module/list/where.cs:42,50` and `range.cs:35` (these sit literally inside `Context.Ok(...)` — the context is in hand, just not passed), `new type("image")` at `type/image/this.cs:93`, and `new type("file")` at `type/file/this.cs:53`.

3. **Residual `!` sweep — acceptance cleanup.** Once Stages 1–4 land, the null-forgiving `!` on already-non-null members is dead noise: `Context.App!` (16 sites) and `ctx.Context!` (6 sites — `ReadContext.Context` is already non-null). Remove them. The two counts reaching zero is a clean completion check for the branch.

**Dependencies:** All three items sit on Stage 1 (App/Context non-null) and Stage 3 (`type.Context` flip). Natural sequence: after Stage 6, since this stage closes gaps the proof assumed away.

## Design

The A–D mechanisms swept the fields that *hold* a context as state and the construction windows around them. They missed two shapes that aren't plain state fields, plus the bang residue:

- **A read-through getter (item 1).** `context.CallStack` isn't a state field; it forwards `App.CallStack`. The plan correctly said "don't blanket-flip getters" — but this one's upstream (`App`, and `App.CallStack`) is now provably non-null, so the audit lands on "flip it." `?.Current` stays because *that* upstream really is absent between calls.

- **A capability vs a field (item 2).** The `type` entity conflates two things the flip must keep separate: the **field** `Context` (flips to non-null, mechanism B) and the **static identity answer** (`GetPrimitiveOrMime`, stays). They're orthogonal; the static table is real engine knowledge, not a nullable workaround.

No new verb+noun surface and no decomposition: item 1 is a getter body, items 2–3 are deletions. OBP-clean.

Cross-reference: `plan.md` mechanism B (`type.Context`); `plan/demolition.md` section B and the stays-list. This stage refines B's `type` row and resolves the deferred `CallStack` getter.

## You own this

The coder owns shapes, site lists, and sequencing. The contract: after this stage, `context.CallStack` is non-null, the `type` static identity answer survives the `Context` flip, and no `Context.App!` / `ctx.Context!` remains. The channel Actor fields are out of scope here — they're settled on `duplicate-execution-paths`.
