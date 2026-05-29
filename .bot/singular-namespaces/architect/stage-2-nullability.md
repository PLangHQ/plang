# Stage 2: Non-null `app` and `context`

> Code/paths here are suggestions that pin the shape — you own the final form. See "You own this" in `plan.md`.

**Goal:** Establish that `app.@this` and `actor.context.@this` are never null at runtime — make the nullable App back-references and Context fields non-null, remove the ~39 defensive `?.` on them, remove the static type-resolution fallbacks they justified, flip the additional over-nullable structural back-references, and rename the local `ctx` → `context` everywhere.

**Scope.** Included: the back-ref/field non-null flips (App + Context + the 5 structural back-refs), the `?.` removals, the `?? GetPrimitiveOrMime`/`?? GetTypeNameStatic` external-fallback removals, routing the one no-context type site through app, the `ctx`→`context` rename, and fixing whatever un-stamped-`data` reads this surfaces. Excluded: the rename (stage 1), the accessor reshape (stage 3), the type-entity promotion (stage 4). `app.Parent` stays nullable. The recursive internal `GetTypeNameStatic` calls inside `type/this.cs` stay. The two init-only back-refs (`GoalCall.Action`, `IEvent.Step`) are **not** flipped without a lifecycle check.

**Deliverables:** App back-refs (`goal`, `module`, `error`) non-null; the 9 Context fields non-null; the 5 structural back-refs (`steps→Goal`, `step→Goal`, `channel→Actor`, `channel→Channels`, `channels→Actor`) non-null; ~39 `?.` sites cleaned; `ctx`→`context` (214 identifiers, 36 files); the 4 `GetPrimitiveOrMime` + 3 `GetTypeNameStatic` external fallbacks removed; `getTypes.cs:172` routed through app; any surfaced stamping bug fixed at its producer; clean rebuild + both suites green.

**Dependencies:** Stage 1 (namespaces stable). Must precede Stage 3 (so the accessor work doesn't thread `App?.`).

## Design

Full surface and the back-ref tables are in `plan/nullability.md`. The shape of the work: flip the declarations to `= null!` (late-stamped, non-null annotation), then delete the `?` at every read. This is mechanical *except* where it surfaces a real bug — a `data` whose `.Type`/`.Kind`/`.Compressible` is read before its context was stamped. Today that silently falls to the static branch; after, it throws. **That throw is the bug.** Fix it at the producer (stamp before the read), not by restoring the guard. Expect a small number; they are the payoff.

Read each `?.` site before stripping it — some chain through `App`/`Context` into a member that *is* legitimately optional (`Context.App.Debug?.MaxLength` — `Debug` is off without `--debug`). Strip only the `?` on `app`/`context`/`ctx`; leave the `?` on genuinely-optional downstream members. No blanket find-replace.

**The extra back-refs:** flip the 5 structural ones (a step always belongs to a goal; a channel always has an actor once registered). Hold the 2 init-only ones (`GoalCall.Action`, `IEvent.Step`) — a call or binding can legitimately exist unbound; confirm each against its lifecycle before flipping, and if it can be null in a real transient, leave it nullable. The rule is "never-null-in-runtime → non-null."

**`ctx`→`context`:** one name across the codebase. Mechanical, but do it as its own reviewable change within the stage (it touches 36 files) so it doesn't tangle with the semantic non-null flips in review.

`data.Type` in this stage reads `context.App.Types.Clr(Value)` with the `?.` and the `?? GetPrimitiveOrMime` removed — *not yet* the `context.app.type[Value]` entity form. That lands in Stage 4. Stage 2's job is only to make the invariant true.
