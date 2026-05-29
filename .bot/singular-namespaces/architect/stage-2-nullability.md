# Stage 2: Non-null `app` and `context`

**Goal:** Establish that `app.@this` and `actor.context.@this` are never null at runtime — make the three nullable App back-references and the nine nullable Context fields non-null, remove the ~39 defensive `?.` on them, and remove the static type-resolution fallbacks they justified.

**Scope.** Included: the back-ref/field non-null flips, the `?.` removals, the `?? GetPrimitiveOrMime`/`?? GetTypeNameStatic` external-fallback removals, routing the one no-context type site through app, and fixing whatever un-stamped-`data` reads this surfaces. Excluded: the rename (stage 1, done), the accessor reshape (stage 3), the type-entity promotion (stage 4). `Parent` on the app root stays nullable. The recursive internal `GetTypeNameStatic` calls inside `types/this.cs` stay.

**Deliverables:** `goals/goal/this.cs:178`, `modules/this.cs:20`, `errors/Error.cs:44` App back-refs non-null; the 9 Context fields non-null; ~39 `?.` sites cleaned; the 4 `GetPrimitiveOrMime` external fallbacks + 3 `GetTypeNameStatic` external fallbacks removed; `getTypes.cs:172` routed through app; any surfaced stamping bug fixed at its producer; clean rebuild + both suites green.

**Dependencies:** Stage 1 (namespaces stable). Must precede Stage 3 (so the accessor work doesn't thread `App?.`).

## Design

The full surface is in `plan/nullability.md`. The shape of the work: flip the declarations to `= null!` (late-stamped, non-null annotation), then delete the `?` at every read. This is mechanical *except* where it surfaces a real bug — a `data` whose `.Type`/`.Kind`/`.Compressible` is read before its context was stamped. Today that silently falls to the static branch; after, it throws. **That throw is the bug.** Fix it at the producer (stamp the context before the read), not by restoring the guard. Expect a small number of these; they are the payoff of the whole exercise.

Read each `?.` site before stripping it — some chain through `App`/`Context` into a member that *is* legitimately optional (`Context.App.Debug?.MaxLength` — `Debug` is off without `--debug`). Strip only the `?` on `app`/`context`/`ctx`; leave the `?` on genuinely-optional downstream members. No blanket find-replace.

`data.Type` in this stage reads `context.App.Types.Clr(Value)` with the `?.` and the `?? GetPrimitiveOrMime` removed — *not yet* the one-line `context.app.type[Value]` entity form. That form lands in Stage 4. Stage 2's job is only to make the non-null invariant true so Stage 4 can rely on it.
