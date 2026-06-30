# Non-context nullable objects — the actor / back-reference spine

## Why

Same root cause as the `context-never-null` branch: a `?` (or a `null!`) that is never actually null at runtime forces `!`, `?.`, and `?? fallback` choreography on every consumer — complexity with no payoff. That branch owns the **context** nullables. This doc owns the **non-context** ones the same audit surfaced — mostly actor references and `null!` back-references wired once at construction and read non-null forever.

The shape comes in two forms, both the same disease:
- **`?` phantom** — declared nullable, never null. The classic `context-never-null` shape.
- **`null!` lie** — declared non-null, seeded `= null!` for two-phase construction, and consumers still `?.` it because they don't trust the declaration. In this codebase the `null!` form is the bigger cluster.

## Channel Actor fields — decided

`channel.@this.Actor` (`channel/this.cs:72`), `channel.@this.Channels` (`:80`), and `channel.list.@this.Actor` (`list/this.cs:35`) are declared `= null!` today, with a `_app.System.Context` fallback for "service-owned channels have no Actor" (`list/this.cs:42`: `Actor?.Context ?? _app.System.Context`). That premise is wrong.

**Decision (Ingi):** Service **is** an actor. The App is born with **2 actors (system, user) and 3 channels**; the `service` actor is **transient** — created on demand (e.g. an HTTP response that needs its own execution/warning/error context) and disposed when done. A service-owned channel carries that transient service actor for its whole life, so `Actor` and `Channels` are never null.

- Flip all three to genuinely non-null. Delete the `_app.System.Context` fallback (`list/this.cs:42`), every `Actor?.` / `Channels?.App` / `Actor?.Context` defensive site (`channel/this.cs:185,196,248,250,296,301,314`; `list/this.cs:137`), the "Service-owned Channels have no Actor" comments, and the "firing with no Actor — handlers receive null context" debug path (`channel/this.cs:250`). The service actor is created+disposed as a unit with its channel(s), so there is no live channel with a dead actor.
- **Deferred (Ingi, "for later"):** `channel.@this.Channels` (the back-reference to the parent collection) should be named for its type — `channel.list`, not `Channels`. Naming only; the non-null flip stands regardless.

This was originally drafted into `context-never-null` Stage 7; moved here because it's actor work, not context.

## The rest of the cluster — surfaced, not yet decided

The audit found a coherent set of sibling actor / back-reference nullables. Listed here so the channel decision sits with its kin; each still needs its own call before coding.

**The `null!` back-reference spine** — wired once at parse/construction, read non-null forever:
- `Goal.App` (`goal/this.cs:185`) — the root of the builder's `(goal.App ?? app)!` choreography in `validateResponse.cs` (incl. a `…!.User.Context!` double-bang). Flipping it lets the fallback `app` param drop entirely. Strongest single win.
- `Step.Goal` (`goal/steps/step/this.cs:116`) — **17** `.Goal?.` sites; declared `null!` yet universally `?.`'d. **Needs a decision**, not a reflex flip: can it be born non-null at parse time, or is the honest answer `Goal?`? (The split personality is the smell either way.)
- `goal.list.App` (`goal/list/this.cs:24`) — `null!` constructed once with 3 dead defensive guards (`:264,336,344`).
- `Steps.Goal` + `Steps.Context` (`goal/steps/this.cs:15,18`) — the propagation source for `Step.Goal`; clean up together.
- `Actions.Step` (`goal/steps/step/actions/this.cs:24`) — set by the `Step.Actions` getter; borderline late-init.
- `actor.Permission` (`actor/this.cs:55`) — `null!` private-set always assigned in ctor; trivially `{ get; }`.
- `CurrentActor` (`this.cs:245`) — ctor always assigns `= _user`; reorder construction and drop the `null!`.
- `module.@this.App` (`module/this.cs:20`) — genuine construction cycle (App needs Modules, Modules needs App); leave `null!` but normalize the 4 `App?.` reads to `App.` so it stops self-signaling nullability.

**The `_item` inverse fix** (`data/this.cs:490`) — `_item` is declared **non-null** (`= null.@this.Instance`) per its own invariant, but one assignment in `SetValueDirect` leaks raw `null`, forcing 7 guard sites (`Peek`/`Type`/`Kind`/`ToBoolean`/`Navigation`/…). One-line fix: assign `null.@this.Instance`. Deletes 7 guards.

**Collections → `= new()`:**
- `Data.Warnings` (`data/this.Result.cs:62`) — collapses 4 identical `!= null ? clone : null` ternaries.
- `BuildResponse.Errors` / `Warnings` (`module/builder/BuildResponse.cs:13,14`) — `Step.Errors` is already non-null and proves the wire tolerates empty.
- `LlmMessage.ToolCalls` / `Images` (`module/llm/LlmMessage.cs:21,29`) — **conditional**: verify the OpenAI request serializer omits empty collections before flipping, or null carries "omit from body" meaning.

## You own this

The file:line references and code shapes here are findings, not prescriptions — the coder owns the final shape, names, and sequencing, including how the transient `service` actor is constructed and disposed around an HTTP response. The one settled design fact is the channel decision (Service is an actor → those fields non-null). `Step.Goal` is explicitly an open call. Everything else is a candidate, not a contract.
