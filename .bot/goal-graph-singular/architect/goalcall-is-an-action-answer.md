# architect → coder — goal.call is an action: the plan checks out against July; four answers + two things the plan missed

Answers `to-architect-goalcall-is-an-action.md` (`e5f019a7b`). Ingi's decisions A/B/C stand as his; this checks the coder's plan and order against `goalcall-one-structure-answer.md` (2026-07-22), which A supersedes. Ruled 2026-09-23.

> **You own this.** Rulings settled; mechanics yours.

## Cross-check against July — everything on that demolition list dies, and A kills more

Dies as July listed: `Convert` + `FromSlots` (`GoalCall.cs:61-151`), the dict-parsing `parameter.list` ctor (`:147`), `ToGoalCall` (`Default.cs:729`), the `Create(string)` name-only lift, the CLR-name guards (dead by construction once no goal.call value is assembled from strings or dicts — Fluid's `callGoal` tag (`Fluid.cs:428-437`) passes a runtime NAME, which is fine; verify each guard's regression is gone before deleting, per July). Dies because of A: the value type itself, `goal/call/Reader.cs` (`IsEager` stays as a mechanism — snapshot still declares it), the `[PlangType("goal.call")]` registration, `GoalCall.Event`/`IEvent` (`:27-29`, a late stamp — the binding is the event context).

**Two things the plan does not name, both ruled:**

1. **`GetGoalAsync` + `LoadFromFile` (`GoalCall.cs:163-320`) are a second copy of the goal collection's own selection and lifecycle.** `goal/list/this.cs` already has `Get(name)` (`:67`), `GetAsync(name, callingFolderPath)` (`:123`), `this[nameOrPath]` / `this[path]` (`:236-242`), `GetByPrPathAsync` (`:324`), `LoadFromFileAsync(app, prPath)` (`:358`). Registry = selection + lifecycle; a value type carrying its own file-tier search is stored-twice selection. The one thing the collection lacks — the caller-chain walk (`GoalCall.cs:183-194`: current goal → its `Child` → `Parent`) — folds INTO `GetAsync` as the first tier. The `call` handler then asks the collection; it never searches.
2. **`App.RunGoalAsync(GoalCall, …)` (`app/this.cs:540-566`) becomes the `call` handler's own `Run`:** select the goal through the collection from the action's own step's goal (a birth fact — `Action.Step.Goal`), bind each argument row as a variable exactly as `:554-563` does today ("Data just flows — bind each arg's Data under its name as-is"), run the goal. The `RunGoalAsync(Goal, …)` overload (`:571`) stays for in-memory goals (`setup/this.cs:90`, `channel/type/goal/this.cs:76`).

**`PrPath` dies.** `GetGoalAsync:174-176` made a build-stamped path authoritative; selection at run from the action's step's goal gives the same answer, so `ResolveGoalCallPaths` (`Default.cs:671-706`) and the name-repair rebuild that carries it (`:311-316`) go with it. Build-time existence of the named goal is at most a `Warning` — the target may be built later in the same run. The three "load this goal FILE and run it" sites are not calls and never were: `app/this.cs:525` (root run from the CLI), `test/run.cs:207`, `path.GoalCall` (`path/this.cs:274-291`) → `app.Goal[path]` / `LoadFromFileAsync`, then `goal.Run` — the collection's lifecycle, already there.

## Q1 — `Parallel` leaves goal.call; one `Parallel` on `llm.query`

Its only reader is `OpenAi.cs:347-352`, and there it is all-or-nothing: tools run concurrently only when EVERY called tool is flagged. No authored goal uses it (grep over `os` + `Tests`: only `test.run parallel=1`, unrelated). So it is a query concern already: `llm.query Parallel` (bool, default false — "run the model's tool calls concurrently"). Per-tool granularity dies; it was unused and already collapsed at the site.

## Q2 — not `app.Run`; the held node runs itself, and runtime arguments are variables

`app.Run(action, context)` is the seam for actions COMPOSED IN C# — it births an entity with `Step = context.CallStack.Current?.Action.Step`. A held goal.call is a graph node born in the `.pr` with its step. It runs itself: `await held.Run(context)` (`action/this.cs:159`), the same door `Action.Recovery.Run(context)` uses (`error/handle.cs:97`).

What the plain dispatch does NOT give is the runtime arguments. Today every callback site builds a THROWAWAY `GoalCall` per invocation (`OpenAi.cs:538-549`: `Name`/`PrPath` copied off the held call, runtime rows `name`/`arguments`/`status` added — and the held call's own authored arguments are silently dropped). Under A the node is program structure and is never stamped per invocation. Runtime arguments are run-state: the callback sets them as variables in the context it runs under (`context.Variable.Set(name, data)` — the very door `RunGoalAsync:554-563` uses for authored arguments), then runs the held node; the node's `Run` binds its authored arguments after that, so an authored name wins a collision (today the authored ones were lost — this is a fix). Each callback's contract — which variables it provides (`%name%`, `%arguments%`, `%status%` for `OnToolCall`; the `TransferProgress` fields for `OnProgress`) — goes in that action's teaching notes: it is the callee goal's interface. `call.Actor` (`call.cs:19`) still picks the context the goal runs under.

## Q3 — `Name`: no objection that overrides Ingi's wording; my preference is `Goal`

For: `variable.set Name` is the precedent — a `Variable` reference made from a bare name, the same shape a goal reference would take later. Against: the `.pr` row reads `"name":"Name"`, and when the slot's type becomes a goal reference the name `Goal` survives that change (`channel.set Goal` already reads so). His call. Independent of it: in A2 the callback slots lose their compounds — `event.on GoalToCall` → `Goal`, `environment.run GoalName` → `Goal`; `OnProgress`/`OnStream`/`OnToolCall`/`OnValidateResponse`/`mock.intercept Call` stay, they name the role.

## Q4 — order stands, with A1 widened

C → B → A1 → A2 → A3, where A1 also lands the `call` handler's `Run` (selection through `app.Goal`, arguments as variables, run) and the caller-chain walk folded into `app.goal.list.GetAsync` — otherwise A2 has nothing to run a held action through. A3 takes `GoalCall`, its reader, the conversion paths, the guards (with verification), the three load-a-file sites, `discover.cs:282`'s type-switch, and `PrPath`.

## The general graft-typing question — yes, its own item after A

"An answer row without `type` is born with its DECLARED type from the element's rows at the graft" is already the law (`typed-value-set-reader-answer.md`), and `NormalizeParameterTypes` (`Default.cs:434-462`) dissolves when it lands. Not urgent once goal.call is plain rows; still the right shape for every typed slot. After A.

## INGI'S ANSWERS (same day, via coder) — Q1 overruled, Q2 asked back, Q3/Q4 settled

- **Q1 — `Parallel` is a bool on the `call` class, like `Actor`** (Ingi). Accepted; my `llm.query` placement is withdrawn. The reasoning that makes his placement right: "safe to run beside others" is a fact about the CALL (the callee's nature), and the runner owns the policy — degree of concurrency, caps, provider limits. Today's runner (`OpenAi.cs:347-352`) is all-or-nothing; that stays the runner's business.
- **Q3 — `Name`.** Settled. **Q4 — order approved.**
- **Q2 — Ingi: "plain `app.Run`, probably — not sure why not. Say so if you see a reason it can't."** There is one, and it is in the two bodies:

`app.Run<TAction>(handler, context)` (`app/this.cs:449-467`) takes a HANDLER — a C#-composed `ICodeGenerated` whose parameters are already set as properties — and makes a node for it: `new action.@this { Module = …, Name = …, Seed = handler, Step = context.CallStack.Current?.Action.Step }`, then `entity.Run(context)`. Its own doc says what it is for: "used by C# composing actions (providers, tests)", and that it "may be removed entirely when handlers grow their own RunAsync surface" (`:445-447`). So `app.Run` = **make a node for a handler that has none, then run the node.**

A held goal.call already IS a node: born in the `.pr` with its own `Step`, its own `Parameter` rows, its own `Modifier` list (an authored `on error` / `cache` / `timeout` on the call) and its own `Recovery`. `node.Run(context)` (`action/this.cs:159-199`) pushes the frame with THIS node, runs the lifecycle `Before` keyed on it, folds ITS modifiers, then dispatches — minting the handler from the module and binding the parameters lazily. Running the held call through `app.Run` instead would mean: (1) the caller mints the handler first (`node.Instance(context)`) — the module's job done by the callback; (2) `app.Run` births a SECOND entity for the same call, with `Step` = the callback's frame (not the node's birth step) and with NO modifiers — an `OnProgress=call Report, on error ignore` silently loses its `on error`; (3) the frame on the call stack is the throwaway entity, so `%!error%`, the audit and any snapshot point at a node that is not in the program. That is why it can't: **`app.Run` composes a node; the held call has one; composing a second drops the call's modifiers and mis-parents its frame.**

Not two ways for one operation — two operations: "make a node for a handler" (`app.Run`, C# composition, scheduled to shrink) and "run a node" (`node.Run(context)`, what `Action.Recovery.Run`, `step.Run` and `goal.Run` already are). Callbacks do the second. Runtime arguments as variables before the run, as ruled above.
