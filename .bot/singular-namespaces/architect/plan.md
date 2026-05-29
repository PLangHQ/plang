# Singular Namespaces — Plan

## Why

`PLang/app/**` is inconsistent: some folders are plural (`goals/`, `channels/`, `variables/`, `errors/`, `events/`, `formats/`, `types/`), some are PascalCase (`builder/Types/`, `tester/Test/`, `http/Response/`, `Services/Service/`). The deeper problem the inconsistency points at: **plurality is a property of a collection container, not of an entity.** You always work with one object at a time. So an entity's home should be singular, and the collection over it should be a child node of the entity — not a plural sibling.

The visible symptom is four wrapper aliases on the app root: `AppGoals`, `AppChannels`, `AppEvents`, `AppModules` (`app/GlobalUsings.cs:3,13,23,26`). Each is `{App}{Plural}` — a *collection* wearing the costume of a singular App capability, sitting beside the entity instead of below it. The old "Law of Names" treated `AppGoals` as canonical; this branch reverses that. Fixing the names properly drags in three more things the design conversation surfaced, and we are doing all four in one branch:

1. **Rename** `app/**` to singular + lowercase.
2. **Reshape** `app.X` from a wrapper into a collection node with verb-less selectors — and kill the four `App*` aliases.
3. **Non-null invariants** for `app` and `context` (drop the defensive `?.` and the static type-resolution fallbacks they justify).
4. **Promote `type.@this`** to a real entity owned by `data.Type`.

## You own this

Every code snippet in this plan and its stage files is a **suggestion that pins the shape, not the final text**. The coder owns method bodies, names at the margin, and how the migration is sequenced within a stage. If a cleaner form preserves the contracts below, take it. What is *not* negotiable: the accessor surface (`app.X` is the collection node; `["name"]` selects; `.list` enumerates; `.current` only where execution has a current), the registry/element split (selection+lifecycle on the registry, behavior on the element), and the non-null `app`/`context` invariant.

## The model (settled)

`app.X` is the **collection node** for concept `X`, owned once by the singleton app (or by `actor` for channel). It is never a flat `App<Plural>` property and never lives on the element.

```
app.goal            → the collection            (goal.list.@this)
app.goal["Start"]   → select by name            → goal.@this
app.goal.list       → enumerate
app.goal.current    → the goal I'm in           (reads CallStack.Current.Action.Step.Goal)

app.type            → the collection            (type.list.@this)
app.type["int"]     → select by name
app.type.of<int>()  → select by clr type
app.type.list       → enumerate
                      (no .current — nothing is ever "in" a type)
```

Rules that fell out of the design walk (full reasoning in `plan/accessor-model.md`):

- **There are no "entities vs services" — only services, some with a `.current`.** Goal and type are both collections you select from. The one difference is that execution flows *through* goals (so there is a "current goal", read from the callstack) but nothing is ever *inside* a type. `.current` is that single difference, present only where a current exists (goal yes; type, channel, event, module, format no).
- **Registry = selection + lifecycle; behavior lives on the element.** `X.list.@this` does `[name]`, `Add`/`Remove`/`Contains`, enumerate — nothing else. A type-switch inside a registry (`if (channel is channel.stream.@this)`) is behavior in the wrong place: push it onto the element as a virtual member. See the channel I/O example in `plan/accessor-model.md`.
- **`data` owns its type.** `data.Type => context.app.type[Value]`. No raw `System.Type` floating free; the value that has a type is the door to it.
- **`app` and `context` are never null.** Drop every defensive `?.` on them and the static fallbacks (`GetPrimitiveOrMime`, the `App?.Types... ?? GetTypeNameStatic` pattern). A null-ref is a stamping bug fixed where the stamp was missed, not guarded against everywhere. (`Parent` on the app root stays nullable — the root app legitimately has no parent.)
- **Folder names are lowercase**, except the C# infrastructure folders that are not domain concepts: `Attributes/`, `Diagnostics/`, `Statics/`, `Utils/` stay PascalCase (per `/CLAUDE.md`). `Services/Service/` *is* in scope and lowercases to `service/` (the brief lists it). `filesystem/Default/` is the keyword carve-out and stays as-is.

## Cross-cutting decisions

- **Index-miss is a defined policy, not a silent redirect.** `app.channel["nope"]` / `app.goal["nope"]` behavior (error / noop / create) is governed by a setting with a chosen default — not an implicit noop and not a hard throw baked into the indexer. Detail in `plan/accessor-model.md`.
- **The generator is string-typed and the compiler won't protect it.** `PLang.Generators/Emission/Action/this.cs:177` holds `const string prefix = "app.modules."` and the emitted-code templates contain namespace literals (`app.channels.channel.@this`, `app.goals.goal.steps...`, `app.errors.ServiceError`, ...). `Discovery/this.cs` matches on `"app.modules"` string predicates. These fail at *consumer* compile time, not in the generator. They are in `stage-1` and must be verified by a clean rebuild, not assumed.
- **Module is the deliberate exception.** `modules/` → `module/`, but the flat action registry `modules/this.cs` is removed from `app.@this`'s public surface and becomes `module/registry.cs` (held on an internal field; 6 call sites reach it through that). Action modules are dispatched, not navigated — no `app.module.current`, no fake singular. See `plan/accessor-model.md`.
- **Build-green strategy: subsystem-by-subsystem within the rename.** Because consumers go through aliases, renaming one subsystem (move folder → fix its namespace → fix that alias's RHS → fix its few direct refs + generator strings) keeps the build green per increment. Don't attempt the whole tree in one non-compiling sweep. Order detail in `stage-1-rename.md`.

## Stages

| Stage | File | What | Size |
|-------|------|------|------|
| 1 | [Rename](stage-1-rename.md) | Folders + namespaces + both `GlobalUsings.cs` + generator strings/templates + test mirrors + doc refs. Singular+lowercase. Old property names + nullable shape unchanged. | ~190 files, alias-shielded |
| 2 | [Non-null invariants](stage-2-nullability.md) | `app`/`context` non-null; drop ~39 defensive `?.`; remove the external static fallbacks; route the no-context type sites through app. Surfaces un-stamped-Data reads. | 3 back-refs, 9 ctx fields, ~39 sites |
| 3 | [Accessor reshape](stage-3-accessor.md) | `app.X` → collection node; rename properties (`Goals`→`goal` …); add `[name]`/`.list`/`.current`/`.of<T>()`; registry=selection+lifecycle (push channel I/O to element); module demote; kill the four `App*` aliases; migrate ~286 call sites. | The heart |
| 4 | [Type entity](stage-4-type-entity.md) | Promote `type.@this` to a rich entity (name+clr+scheme+valid-values), consolidating `System.Type` + `builder.Types.Entry` + scheme; `data.Type` returns it. | Reshapes builder schema path |

Dependency order is strict: 1 → 2 → 3 → 4. Each stage should leave the build green and tests passing before the next begins (rebuild clean — `plang --test` runs off a pre-built binary; see the stale-binary trap in `/CLAUDE.md`).

## Deep dives

- [accessor-model.md](plan/accessor-model.md) — the `app.X` model, the registry/element rule, `.current`, index-miss policy, module exception, channel I/O worked example.
- [rename-map.md](plan/rename-map.md) — the corrected complete rename table, both GlobalUsings, generator strings, test mirrors, doc references.
- [nullability.md](plan/nullability.md) — the non-null surface, what's removed vs what stays, the stamping invariant.
- [type-entity.md](plan/type-entity.md) — the `type.@this` promotion and `data.Type`.
- [test-strategy.md](plan/test-strategy.md) / [test-coverage.md](plan/test-coverage.md) — for test-designer.
