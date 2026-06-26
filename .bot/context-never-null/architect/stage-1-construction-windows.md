# Stage 1: Construction-order non-null (foundation)

**Goal:** Remove the nullability that exists only because of two-phase init inside a constructor. After this stage, every actor and every context exists non-null from construction, and an unknown actor name is a throw — the foundation the rest of the branch assumes.
**Scope:** Mechanism A in the plan, plus `GetActor`. Included: `Context.Actor`, `Variables._context`, `App._system`/`_user`, `GetActor`. Excluded: value-type context (Stage 3), serializers (Stage 4), `Step.Context` (Stage 2).
**Deliverables:**
- `actor/context/this.cs` — `Actor` becomes non-null (`public actor.@this Actor { get; }`), set from a `owner` ctor parameter. `RegisterContextVariables` drops the `Actor?` / `Actor!` (`:171-172`).
- `actor/this.cs` — pass `this` as `owner` into the Context ctor (`:108`); delete the separate `Context.Actor = this` line (`:109`).
- `app/this.cs` — `_system` / `_user` non-null via eager construction at App start; `GetActor(string name)` returns non-null `actor.@this` and **throws** on an unknown name (delete the `(actor.@this?)null` at `:259`).
- `variable/list/this.cs` — `_context` non-null (stamped at construction).
- Drop the `?` / `= null` on parameters in these files as their call chains become non-null.
**Dependencies:** None. This is Stage 1.

## Design

The nullable here was never a real state — it was the one-line gap between constructing Context and assigning its Actor. Production builds Context in exactly one place (the Actor ctor), so passing `this` into the ctor closes the gap. `this` is legal to hand a child ctor because Context doesn't dereference Actor during construction — the `!channels` / `!serializers` registrations are lazy lambdas.

`GetActor` throws because the actor set is closed and hardcoded (system / user / service); an unknown name is a critical miss, not a soft one. Keep returning the existing error-tuple shape *only* if a caller genuinely needs the error-as-value; otherwise throw and return a bare non-null `actor.@this`.

No behavior changes for any passing test — this is pure nullability tightening. If a test breaks, it was relying on a half-built actor/context, which is the bug.

Full detail + the leaf trace: `plan.md` mechanism A and the leaf-trace section; `plan/demolition.md` section A.

## You own this

The ctor parameter name (`owner`), whether `GetActor` throws vs keeps a tuple, and the exact eager-construction point in `App` are yours. The contract: `Context.Actor`, `App._system/_user`, `Variables._context` are non-null types, and `GetActor` never returns null.
