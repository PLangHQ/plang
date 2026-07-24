# architect → coder — node lists: program structure is context-free; context travels with the ask

Answers `to-architect-node-list-values.md`. Settled with Ingi 2026-07-24. Your distillation of the question ("what is the lifecycle of a loaded-from-.pr goal graph w.r.t. context?") was the right question. The answer is neither A nor B.

> **You own this.** The ruling (the law, the two-lifecycle base, what must never happen) is settled; ctor shapes, audit mechanics, and naming are yours.

## The law — third appearance on this branch

**The graph is the program; context belongs to the run. Program structure never stores a context — context travels with the ask.**

The codebase already states it, in the action's own Run comment (`action/this.cs:135-136`): *"Context travels as parameter — actions are shared objects, not per-request."* Same law that ruled errors-are-run/warnings-are-build (area-1b) and parse-at-the-boundary (goal.call).

## Why A and B are both wrong

- **A (stamp context on step/goal at load):** a goal loads ONCE into the `app.Goal` registry and is executed by many actors concurrently (web server, keepalive). Stamp an actor context onto shared structure and two concurrent requests share one slot — the second stamp wins, the first actor's `%var%` resolution reads the wrong actor. Cross-request contamination. A correctness bug, not a style call.
- **B (deferred context on the node lists):** the same bug with worse timing, plus the late-stamp smell.

## Context-never-null is NOT broken — it is restated

The rule is about **use**, not storage: no door may ever RUN with a null context.

- Run-born values satisfy it **by birth** — a list parsed out of an LLM response belongs to exactly one run; born with that run's context; the `:103` throw stays for the public value ctors.
- Program structure satisfies it **by parameter** — every context-needing door receives the ask's context (`Run(context)`, the navigation binding, `ReadContext.Context`). Null stored, never null used.
- What the rule was written to kill stays dead: no `Context ??=`, no use-then-hope. A program-node door with no context in hand fails loud.

## The shape — one base, two lifecycles

`list.@this` conflated two lifecycles under one ctor. Split them:

```
list.@this  (run-born value)      born WITH the run's context — public ctors keep the :103 throw
node lists  (program structure)   born context-FREE (protected ctor); _context null forever;
                                  every context-needing door takes the ASK's context
```

`action.list : list<action>`, `step.list : list<step>`, `parameter.list : list<data>` — real subclasses the reader returns directly (Ingi's ruling stands). The node keeps its own `Run`/`IndexOf` (items are responsible for themselves). `property.list` is the shape precedent, minus any stored-context assumption it carries.

Consequences that just work again, unchanged:
- the empty field defaults (`new(new List<ActionEl>())`, `step/this.cs:49`, `goal/this.cs:43`, `action/this.cs:29,:45`) — an empty program node needs no context to exist
- `Nest` (`step/this.cs:101`) and `Merge` (`:191`) rebuilds
- the `.pr` step reader's double-build dies: the reader constructs the node directly (context-free) and fills elements

## The seven-site audit (the concrete worklist)

Every `_context` use in the base (`type/item/list/this.cs`): `:30` (enumerator minting Data rows), `:115`, `:118` (Row), `:137` (AddRaw), `:312` (Add), `:322` (Insert), `:472`. For each, one of:

1. an ask context is already in reach (parameter, the binding's Data, `ReadContext`) → prefer it, or
2. the door is unreachable for program nodes → document that, or
3. neither → the door's design is wrong for program nodes; bring it back to architect.

No door reachable from a program node may REQUIRE `_context`.

## Never stamp the shared instance

The base's `Context` setter propagation (`:148-163`) and Data's bind-stamp onto `IContext` items are the **run-value** mechanism. For a shared graph node they are the contamination channel — they must never fire on program structure. Where navigation needs a contextful Data-row face over a node list, mint a **fresh run-scoped view over the same elements**. Exact precedent, registry: *"A fresh, cheap wrapper per ask over the same cached elements"* (`module/list/this.cs:155-156`). Shared elements, per-ask wrapper, the shared thing never mutates.

## The reader does not stamp either

At read time `ReadContext.Context` exists — but it is the **loader's** context, not the future runner's. The reader fills elements into a context-free node. Same for the runtime parse (`set %goal.step[i].action% = …`): the node is built within a run, but its destiny is the shared graph — the run that built it is not the run that will execute it. Load context and run context were never the same thing; the old design just never had to notice.

## Scope (confirmed)

- **In:** `action.list`, `step.list`, `parameter.list` — the `[Store]` sequence nodes that ride readers.
- **Out:** `goal.list` (the registry — a cache + name index, reached as `app.Goal`, never reader-produced), `callstack/*`, `error/trail`, `warning/list` (runtime infra).

## Your landed-but-uncommitted work

All compatible; one reshape: the list reader returns the NODE directly (context-free birth + fill) instead of a generic `list.@this` — that turns your red proof test (`ClrJsonActionsArray_GoalCallParam_ReadsAsTypedGoalCall`) green at the assignment, since the produced container IS `action.list.@this`. `ContainerFamily` recognizing `IReadOnlyList<T>` and the element-reader loop stand as built.

## Verify before writing

1. Which base doors are actually reachable from a program node (drive the graph: .pr load, navigation ask, Run, Nest/Merge) — that bounds the seven-site audit to the real subset.
2. The generic-value path is untouched: an LLM-born `list.@this` still borns with context, still stamps its rows (`:115`), still propagates on bind.
3. The proof test goes green end-to-end: json array → typed `action.list` → nested `goal.call` param is a typed `GoalCall` on the assigned step.
4. Concurrency smoke: two contexts navigating the same loaded goal's `action.list` — assert neither sees the other's context on its Data rows (this is the test that makes the never-stamp rule enforceable, not aspirational).
