# Architect — singular-namespaces

## 2026-05-29 — Plan carved: rename + accessor reshape + non-null + type-entity, one branch

**What this is.** The brief asked for a folder/namespace rename of `PLang/app/**` to singular + lowercase. The design conversation with Ingi grew it: the four `App*` wrapper aliases (`AppGoals`, `AppChannels`, `AppEvents`, `AppModules`) are OBP violations — a collection wearing the costume of a singular App capability — so fixing the names properly means reshaping `app.X` into a collection-node accessor, making `app`/`context` non-null, and promoting `type.@this` to a real entity behind `data.Type`. Ingi chose to do all four in one branch.

**The model we settled.** `app.X` is the **collection node** (`X.list.@this`), owned once by the singleton app (or actor for channel) — never a flat `App<Plural>` property, never on the element. `app.X["name"]` selects, `.list` enumerates, `.current` exists only where execution carries a current (goal, read from the callstack — not type/channel/event/module). There are no "entities vs services," only services, some with a `.current`. Registry = selection + lifecycle; all behavior lives on the element (a type-switch in a registry is misplaced behavior). `data` owns its type. Two design knots were worked through to get here: (1) bare `app.goal` can't *be* the current goal without orphaning the collection — one clean name per concept holds one occupant, so the current rides as `.current`; (2) "the goal I'm in" is a context fact (callstack), which is why it can't live as a flat property on the shared app.

**What was done.** Verified the brief against the live tree (found two errors), fanned out four read-only scans for the inventory, and wrote the plan: spine (`plan.md`) + four deep dives (`plan/accessor-model.md`, `rename-map.md`, `nullability.md`, `type-entity.md`). (Stage files and test-handoff files were removed during the design review at Ingi's request, then recreated afterward with the review changes baked in.) Brief corrections captured: `Attributes`/`Diagnostics`/`Statics`/`Utils` stay PascalCase (C# infra); `serializers/` collapses to `serializer/` and `channel/events/`→`channel/event/`. The generator's string-literal namespace refs and `prefix = "app.modules."` tracking constant are flagged as the failure the compiler won't catch.

**Code example — the shape the whole plan turns on:**
```
app.goal["Start"]   → goal.@this        select by name
app.goal.list       → enumerate
app.goal.current    → the goal I'm in   (CallStack.Current.Action.Step.Goal)
app.type["int"]     → type.@this        (stage 4: a real entity, not raw System.Type)
data.Type           → context.app.type[Value]   the value owns its type
// registry = selection + lifecycle; channel I/O moves onto channel.@this.Write(data) — no WriteText, no type-switch
```

**Review done.** Ingi reviewed the plan files and left five comments, all addressed (see `comments.json`): (1) module keeps its `this.cs` — no demote, it's a no-`.current` service at `app.module`; (2) `ctx`→`context` everywhere + a sweep for other over-nullable back-refs (5 structural ones flip, 2 init-only held); (3) `type.@this` is PLang's `System.Type`; (4) `data.Type.ClrType` — the CLR type lives on the type entity. Then recreated the stage files and test-handoff files with those changes baked in. Ready for test-designer.

**Open item** for coder + test-designer: the index-miss policy setting (name + default). The type-entity piece (stage 4) is the integration risk — builder schema must be golden-pinned; it's the natural cut point if it balloons.

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | [Rename](stage-1-rename.md) | pending |
| 2 | [Non-null invariants](stage-2-nullability.md) | pending |
| 3 | [Accessor reshape](stage-3-accessor.md) | pending |
| 4 | [Type entity](stage-4-type-entity.md) | pending |
