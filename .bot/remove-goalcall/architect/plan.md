# Remove `GoalCall` — one door into plang

**Status:** design settled, awaiting Ingi's read-over. Not yet carved into stage files.

> **Coder/test-designer: you own the final shape.** Every snippet and signature below is the architect's suggestion to convey intent, not a contract. If a cleaner shape emerges while implementing, take it — and tell us. The *decisions* (what goes away, who owns resolution, the actor switch) are settled; the *exact code* is yours.

## Why

`GoalCall` and `Goal` are two C# types for one developer concept. A developer never types "goal call" — they write `- call ProcessData, x=%y%`. Yet internally the call site is a `GoalCall` (name + prPath + parameters + parallel + event + action-anchor) that gets resolved into a `Goal` (the AST) at runtime. The two overlap on identity (`Name`, `PrPath` live on both — OBP smell #3) and `GoalCall` has accreted unrelated concerns: event-binding context (`IEvent`/`Event`), an LLM-tool flag (`Parallel`), and the four-tier name resolution (`GetGoalAsync`).

The fix is not to merge call-site state onto the shared, stateless `Goal` definition — arguments belong to a *call*, not a *definition*. The fix is to move arguments onto the **action** (`goal.call` gets its own `Parameters`), let the goal reference just be a `Goal` (resolved lazily, by name, returning a typed error), and funnel **every** goal invocation through the one `goal.call` path. The payoff is architectural, not cosmetic: the runtime ends up with exactly **one C#→plang bridge**. Resolution has one caller. Run has one caller. No second pathway only the entry point uses. No exceptions to keep in sync.

## The settled design

1. **`goal.call` carries the arguments, not the reference.** The handler becomes three properties:
   - `Goal` — `data.@this<Goal>` (the target, resolved lazily; LLM sees `Goal("Name")`).
   - `Parameters` — `List<data>` (the `x=%y%`, set into the variable scope before the goal runs).
   - `PrPath` — `data.@this<path>?`, `[Store, Out]` but **not** `[LlmBuilder]` (hidden from the model). A build-time cache so runtime skips the file search.

2. **`Goal` stays a pure, shared, stateless definition.** It never holds per-call arguments. Arguments land on the stack via `context.Variable.Set` (the handler's job), exactly as the goal already expects (`RunAsync` takes context as a parameter precisely so goals can be cached/shared).

3. **Resolution moves to the registry: `app.Goal.Load(name, context, prPath?) → Data<Goal>`.** This absorbs both `GoalCall.GetGoalAsync` (the four-tier walk: caller's goal chain → `app.Goal` registry → file fallback, with slash-qualified-name handling) and its private `LoadFromFile` (read `.pr`, wire back-references, match sub-goal by leaf name). It lives on the registry because `app.Goal` already owns `Get(name)` and is the collection node; `app.Goal.Load` pairs with the `app.Goal["name"]` selector — the indexer means "already here," `Load` means "get it here, from disk if needed." Name chosen over `Parse` because `Goal.Parse` is already taken for text→Goal (`goal/this.cs:365`); over `Resolve` because `Load` is transparent to a non-native reader and names the dominant cost (reading the `.pr`).

4. **`Goal` gets a value-conversion hook** (string / path / dict → `Goal`) that calls `app.Goal.Load`, mirroring today's `GoalCall.Convert`. This is what lets a `data.@this<Goal>` parameter resolve a raw `%var%` / name / pre-resolved prPath at first `.Value`. The LLM-type-name guard (reject a CLR type name leaking into the goal slot) moves here too.

5. **The runner is `app.Run(goal, context)`.** This is today's `RunGoalAsync(Goal)` renamed. The `RunGoalAsync(GoalCall)` overload is **deleted** outright.

6. **`Event`/`IEvent` deleted; `%!event%` survives via the firing path.** `EventContext { Step, Phase }` is the data behind `%!event%`. Today it takes a detour: `Events.Stamp` stamps it onto the `GoalCall`, then the source generator's `IEvent` detection copies it into `context.Event` before `Run`. With `GoalCall` gone, the firing path (`binding.Run(context, action, result)` — which already knows the triggering action and phase) sets `context.Event` directly, around the handler run, using the existing save/restore scope at `context/this.cs:289`. The `IEvent` interface, the `Event` field, and `Events.Stamp`'s event-stamping all delete. (Later, parked: per-call `OnBefore`/`OnAfter`/`OnInterval` properties on the action — separate branch, not this work.)

7. **Every call site funnels through `goal.call`. Including Start. No escape hatch.** `app.Start` stops being an exception: after the irreducible bootstrap (`Load()`, channel-verify, actor selection, builder-mode branch), it delegates the entry through `goal.call(Goal: entry, Actor: User)`. System owns the call; User executes. Start is just the *first* cross-actor call — same shape as any deeper one. Go through full `app.RunAction(...)`, not a bare `.Run()`, or the entry skips the action-event/error layer and we've re-created an exception quietly.

## Cross-cutting: the actor / `CurrentActor` correction

Today `goal.call` swaps the *context* to the target actor but leaves the global `app.CurrentActor` as the caller (`call.cs:31` runs on `execContext`; `Events.GetBindings` reads `app.CurrentActor`). That's a latent split — the executing context's actor is the target while the global current actor is still the caller. We close it: **`goal.call(Actor: X)` sets and restores `app.CurrentActor` around the run when `Actor` is given** (save on enter, restore in `finally`, nests LIFO). Then the executing actor and the current actor agree, the rule "while a goal runs as X, X's channels/events/permissions govern" holds, and Start's System→User handoff is just this rule firing for the first time. This is a behavior change for *every* cross-actor call, done deliberately, pinned by a test: User's event bindings fire (not System's) while the entry goal runs.

## Data flow (the movie)

Developer writes `- call ProcessData, x=%y%` → builder emits a `goal.call` action with `Goal = Goal("ProcessData")` (a `data.@this<Goal>` whose stored form is the name) and `Parameters = [x=%y%]`; `Build()` runs `app.Goal.Load` for the static name to validate it exists (warn if missing, matching today) and stamps the hidden `PrPath` → at runtime `Call.Run()`: `await Goal.Value` resolves via the conversion hook → `app.Goal.Load(name, context, PrPath)` → `Data<Goal>` (typed error if not found); loop `Parameters` into `context.Variable.Set`; if `Actor` set, switch `CurrentActor`; `app.Run(goal, execContext)` → `Goal.RunAsync`. The callee's AST never embeds in the caller's `.pr` — only the name (+ hidden prPath) is stored; the full `Goal` materializes in memory at first `.Value`.

## Call-site disposition

The incumbent (`GoalCall`) feeds ~30 production files. Full leaf-trace with each site's disposition: **[call-sites.md](plan/call-sites.md)**. Summary of the buckets:

- **Delete:** the `GoalCall` type, `RunGoalAsync(GoalCall)`, `IEvent`, the `GoalCall` global-using, `path.GoalCall`.
- **Rename/move:** `GetGoalAsync` + `LoadFromFile` → `app.Goal.Load`; `RunGoalAsync(Goal)` → `app.Run`; `GoalCall.Convert` → Goal's conversion hook.
- **Retype params `data.@this<GoalCall>` → `data.@this<Goal>`:** `event.on`, `http.request/upload/download` callbacks, `mock.intercept`, `environment.run`, `channel.set`, `llm.query` (Tools/OnToolCall/OnValidateResponse/OnStream). `goal.call` splits into `Goal` + `Parameters` + `PrPath`.
- **Reroute internal callers through `goal.call`:** `builder`, `test.run`, `llm` (OpenAi tool/stream/validate callbacks), `http` callbacks, `ui` Fluid, `channel/type/goal`, `setup`, `error.handle` navigation anchor.
- **Coordinate with the tools-as-actions branch:** `llm.query.Tools` and where `Parallel` lives (an action property, not a field on the reference). Do not redesign tools here — only take "Parallel is described on the action."

## Stage index

To be carved after read-over. Provisional shape:

1. **Stage 1 — Resolution + run primitives.** `app.Goal.Load`, `app.Run`, Goal's conversion hook. No call-site changes yet; the new methods stand alongside the old. Proves resolution works in isolation.
2. **Stage 2 — `goal.call` reshape.** `Goal` + `Parameters` + hidden `PrPath`, `Build()` validation, the `CurrentActor` save/restore. Migrate the developer-facing `- call` path. `RunGoalAsync(GoalCall)` still alive for the other callers.
3. **Stage 3 — Reroute every other call site.** Callbacks, events, channel, env.run, builder, test, ui, Start. Delete `RunGoalAsync(GoalCall)`.
4. **Stage 4 — Delete `GoalCall`, `IEvent`, `path.GoalCall`, the global-using; move `%!event%` to the firing path; clean the catalog/serializer registrations.**

Open question for Ingi before carving: is the 4-stage cut the right granularity, or do you want Start-through-`goal.call` (the headline) pulled forward so we see the one-door property early?
