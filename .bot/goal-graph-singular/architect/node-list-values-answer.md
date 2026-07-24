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

`action.list : list<action>`, `step.list : list<step>`, `parameter.list : list<data>` — real subclasses the reader returns directly (Ingi's ruling stands). The node keeps its own `Run`/`IndexOf` (items are responsible for themselves). `property.list` is the shape precedent for "a node that IS a list value" — but do NOT copy its typed-rows face: `Items.Select(d => d.Clr<Property>())` is a per-read Clr decomposition, the exact disease the consumer trace below exists to prevent. Elements live typed in the slots; the Data face wraps per ask.

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

## The consumer trace (settled with Ingi 2026-07-24) — no caller escapes to CLR

Ingi's fear, from the last conversion: consumers get *adjusted* (`.Clr()` glue at call sites) instead of *updated* (retyped to the plang type). Every consumer of the three nodes is traced and dispositioned below. The rule in one sentence:

> **When a consumer wants CLR, the fix is the consumer's signature, not a conversion at the call site.** `.list`, `.ToList()`, `.Clr()` on a node list are the same wrong move. CLR appears only INSIDE 3rd-party/BCL boundary bodies (OpenAI request assembly, reflection-invoke internals) — never in a signature the graph flows through.

### No public typed-element face at all (Ingi's ruling)

First instinct was "navigation gets Data rows, the engine gets typed elements." Wrong — the run path never enumerates from outside (`action.list.Run` / `step.list.Run` iterate their own private backing). Every outside loop is either lifecycle that belongs ON the graph, or a query that belongs on the graph's walk door. After those moves, **nobody outside needs typed elements**: the typed face goes internal/protected; the only public enumeration is the value face (Data rows, minted per ask with the ask's context — audit site `:30`). Outside code either *tells* the graph (run, wire, walk) or *navigates* it as values. It never harvests raw elements.

Consequently the CLR escape hatch dies with **zero survivors**:

```csharp
// action/list/this.cs:22 (and its twins) — DELETE, no replacement:
public IReadOnlyList<Action> list => _actions;
```

A caller that thinks it needs the raw collection is a member that hasn't been written yet.

### A — the ~16 `.list` enumeration loops → graph members + the walk door

- **Lifecycle loops become graph members.** The goal-load wiring (`step.Goal = …; action.Synthetic = false`) is written TWICE — `goal/list/this.cs:373-376` and again `GoalCall.cs:286-292` (LoadFromFile). Same choreography at two call sites = a missing member: the goal wires itself at load, once, as its own method. Same treatment for the `Synthetic` stamps and `Merge`'s element copy.
- **Query walks use the graph's own walk door**, which already exists: `goal.ForEachAction((step, action) => …)` (used by `test/discover.cs`). Migrate the hand-rolled walkers onto it: `goal/this.cs:359`, `goal/setup/this.cs:62,65`, `getTypes.cs:56`, `debug/this.cs:286,546`, `mock/intercept.cs:89`, `discover.cs:314` (`.Where(a => a.IsCondition)` — walk door or a real member if it's a question the step should answer), `BuildResponse.Validate.cs:111`, `Default.cs:289`. Naming: `ForEachAction` is a verb+noun compound — pick an honest single verb (e.g. `Walk`); your shape.

### B — nine CLR-typed signatures retype to the node

| Site | Today | Becomes |
|---|---|---|
| `loop/foreach.cs:87` | `List<Action> GetBodyActions()` | returns `action.list.@this` (fresh node — the body slice) |
| `build/code/Default.cs:274` | `Fold(IReadOnlyList<Step> flat, …)` | takes `step.list.@this` (`:265` already passes the node's content) |
| `llm/code/OpenAi.cs:866` | `BuildParamSchema(IReadOnlyList<data.@this>?)` | takes `parameter.list.@this`; lowering to OpenAI's dict shape happens inside the body — the legal boundary |
| `llm/code/OpenAi.cs:561` | `ParseToolArguments(…, goalCall.Parameter, …)` | same retype on its parameter |
| `build/code/Default.cs:654` | `a.Parameter.ToList()` for `ValidateBuild` invoke | the `IBuildValidatable` contract takes the node (becomes an action member under module-owns-action anyway) |
| `build/BuildResponse.cs:15` | `List<Step> Steps` | `step.list.@this` — it holds graph steps; the wire writer lowers at the boundary |
| `module/list/this.cs:278` | `Describe() → List<action>` | returns `action.list.@this` |
| `goal/call.cs:46` | `goalCall.Parameter = kept` via implicit `List<Data>` operator | build the node honestly; drop the implicit-operator reliance |
| `call/this.Snapshot.cs:40` | `IndexOfAction(List<action>, …)` | **dead — zero callers** (Capture uses the node's own `IndexOf` at `:26`). Delete. |

### C — construction sites: build-then-wrap dies, construct the node and `Add` into it

Readers (`step/serializer/Reader.cs:27`, `goal/serializer/Reader.cs:45`, `action/serializer/Reader.cs:70`), the Item.cs Creates (`step:38`, `action:71`, `goal:46`), the `.goal` text parser (`goal/this.cs:384,467,546` — note `step/list:14-17` documents the mutate-after-wrap trick; `Add`-into-node makes both the trick and the raw list unnecessary), copy-construction via node copy-ctor (`new(from.Action)` — `Merge` `step/this.cs:191`, `BuildResponse.Validate:32`), empty defaults via the context-free ctor. `Nest` (`step/this.cs:68,71,101`) is the owner mutating its own node — stays.

## Your landed-but-uncommitted work

All compatible; two reshapes: the list reader returns the NODE directly (context-free birth + fill) instead of a generic `list.@this` — that turns your red proof test (`ClrJsonActionsArray_GoalCallParam_ReadsAsTypedGoalCall`) green at the assignment, since the produced container IS `action.list.@this`. And the `Elements` accessor you added goes **internal/protected**, not public — per the consumer trace there is no public typed-element face; outside code tells the graph or navigates it as values. `ContainerFamily` recognizing `IReadOnlyList<T>` and the element-reader loop stand as built.

## Verify before writing

1. Which base doors are actually reachable from a program node (drive the graph: .pr load, navigation ask, Run, Nest/Merge) — that bounds the seven-site audit to the real subset.
2. The generic-value path is untouched: an LLM-born `list.@this` still borns with context, still stamps its rows (`:115`), still propagates on bind.
3. The proof test goes green end-to-end: json array → typed `action.list` → nested `goal.call` param is a typed `GoalCall` on the assigned step.
4. Concurrency smoke: two contexts navigating the same loaded goal's `action.list` — assert neither sees the other's context on its Data rows (this is the test that makes the never-stamp rule enforceable, not aspirational).
