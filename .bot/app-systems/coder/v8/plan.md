# Stage 8 — `on` on every object: trace and plan (no code yet)

Architect's design: `plan.md` §"Events on each object (stage 8)" (settled with Ingi 2026-09-26) and the stage-8 row.
This file is the coder's trace of what exists today, where each piece lands, the slices, and the questions.
Everything below was traced from source (file:line); the claims the plan leans on were re-read by hand.

## 1. What exists today

**One registry per actor.** `actor/context/this.cs:94` `Events` (an `app.event.list`: a locked `List<EventBinding>`,
`Register` → id, `Unregister(id)`, `GetMatchingBindings(type, goalName, stepText, module, action)`). There is one
context per actor (`actor/this.cs:99`), so bindings are per actor. `app/this.cs:126` `App.Event` is a second, app-level
list that only channels read (`channel/this.cs:196-201`); nothing registers into it in production.

**A binding** (`event/lifecycle/binding/this.cs`) is: `Trigger Type`, the patterns (`GoalNamePattern`, `StepPattern`,
`ActionPattern`, `ChannelName`, `IsRegex`: prefix `*`, Contains for steps, `module.*`), `Handler: Func<ctx, action?,
data?, Task<data>>`, `Call` (the `goal.call` an `event.on` holds), `Priority`, `StopOnError` (default true), `Targets`
(mock's tag). `Run` (43-77) guards re-entry per context, sets `%!event%` to a `moment`, runs the handler, and for
Before/AfterAction consumes `context.EventOverride` into a `Handled` result.

**Firing** goes through `context.LifecycleFor(goal|step|action)` (`actor/context/this.cs:424-485`): a per-node
`Lifecycle {Before, After}` cached in `_eventContainers`, rebuilt on any register (`OnChanged` → clear all).

| Trigger | fires at | note |
|---|---|---|
| BeforeGoal / AfterGoal | `goal/this.cs:348` / `:386` | After not on the CallStackOverflow catch (:391) |
| BeforeStep / AfterStep | `goal/step/this.cs:115` / `:140` | After also after a caught exception |
| BeforeAction / AfterAction | `goal/step/action/this.cs:188` / `:223`, plus one AfterAction **per modifier** `:211-213` | a Handled before-result replaces dispatch (`:192-200`) |
| Before/AfterWrite, Before/AfterRead, OnAsk | `channel/this.cs:120,132,141,152,170` | own path: 3 sources merged (channel, actor, app), no priority sort, StopOnError ignored |
| On{Before,After}{Goal,Step}Load | collected at `context/this.cs:431-433,455-457` | **never fire**: `binding/list/this.cs:28` filters by the running trigger |
| BeforeAppStart, AfterAppStart, OnError, OnVariableChange, OnCacheHit, OnCacheMiss | nowhere | declared only (`event/Trigger.cs`) |

Two quirks worth knowing: the action lifecycle is matched on module/action only, so a `GoalNamePattern` has no effect
on Before/AfterAction (`context/this.cs:471-485`: debug's goal filter at action level is silently ignored); an
AfterAction override is consumed but never replaces `data` (`action/this.cs:223-224`).

**The `event` module** (`module/action/event/`): `event.on(Trigger, Goal, GoalPattern?, StepPattern?, ActionPattern?,
IsRegex=false, Priority=0, Actor?, ChannelName?)` registers on the target actor's context and returns the binding id
(`on.cs:40-67`); `event.remove(EventId)` unregisters on the *current* context (`remove.cs:11`: misses a binding made
for another actor); `event.skipAction(Value)` sets `EventOverride` (`skipAction.cs:16`).

**Modifiers** (the other half of stage 8). A modifier is an action stored on the action before it
(`action/this.cs:51-52` `Modifier`, a `modifier.list` sorted by `[Modifier(Order)]`: `on.error` 0, `cache.wrap` 50,
`timeout.after` 100). `modifier.list.Wrap` (46-64) folds them outermost-first and groups adjacent `on.error` clauses
into one try/catch (`Catch`, 70-92). `on.error` (`module/action/on/error.cs`) filters (StatusCode, Key, Message),
retries (`RetryCount`, `RetryOverMs`), runs `action.Recovery` (the recovery slot, `action/this.cs:66-67`), `Order`
(GoalFirst/RetryFirst), `IgnoreError`. `cache.wrap` (`cache/wrap.cs`: DurationMs, Sliding, Key → `app.Cache`),
`timeout.after` (`timeout/after.cs`: Ms → a linked CTS pushed on the context, 408 on expiry). In the `.pr`: `"modifier":
[…]` on the action and `"recovery"` on the on.error element (`serializer/Reader.cs:73-92`, `this.Item.cs:64-83`); only
two built `.pr` hold one (`os/system/error/.build/show.pr`, `os/system/builder/BuildGoal/.build/start.pr`). In formal:
`Formal.cs:165-171` attaches a modifier to the action before it.

**Consumers of the wiring** (production):
- debug (`module/action/debug/this.cs:138-177`): BeforeStep, AfterStep, AfterGoal, and Before/AfterAction at level
  Action; all on `App.User.Context.Events`, `priority int.MaxValue`, `stopOnError false`, goal filter from the setting.
  Plus C# events: the variable store's `OnCreate/OnSet/OnRemove` (84-86, the watch), OpenAi's
  `OnBeforeRequest/OnAfterResponse` (97, 123).
- channel (`channel/this.cs:117-252`, `channel/event/this.cs`): Before/AfterWrite, Before/AfterRead, OnAsk; per-channel
  list only added to by tests.
- mock (`module/action/mock/intercept.cs:17-79`: a BeforeAction binding with `actionPattern`, tagged with the handle;
  a `Return` value goes through `EventOverride`; `reset.cs` unregisters by id or by tag; `app/mock/this.cs:16`
  `EventBindingId`).
- test (from stage 7's cleanup): `test.Time` (`test/this.cs:128-152`, Before/AfterStep on its App's user context,
  the entry goal's own steps), `coverage.Watch` (`test/Coverage.cs:44-60`, AfterAction; counts modifiers through the
  per-modifier AfterAction), `test.list.Made` (`test/list/this.cs:62,101`, a C# event; only tests subscribe).
- `module/Events.cs` + `goal/step/action.Events` (goal `:31-37`, step `:16-22`, action `:140-145`): no production
  reader; one test (`PlangRuntimeTests.cs:80-101`).
- `GlobalUsings.cs:8-9` (`Lifecycle`, `Bindings`).

**Other "object announces" C# events** (not bindings): variable list `OnSet/OnCreate/OnRemove`
(`variable/list/this.cs:49-59`; subscribers: callstack snapshot `this.Snapshot.cs:39-40`, call frame
`call/this.cs:149-150`, debug), `actor.setting.Written` (`:42`, → `app.Refresh`), OpenAi's two.

**Value births.** The type's door is `type/this.cs:237` `Create(raw, context)` (+ `:320` wire, `:363` data door);
~92 production sites `new` a plang value outside its type (text 20, list 32, dict 12, …; the rest are typed nulls and
infrastructure). Stage 8 makes the door fire `on.create`; stage 9 moves the births onto it.

**Usage in `.goal`** (excluding `.build`): event steps in 7 active test goals + 2 handler goals (`skip action`) under
`Tests/Modules/Event/`, `Tests/Channels/Events/`, `Tests/Modules/Mock/`; `os/system/events/*.goal` is legacy v1 phrasing
(no C# reads it). Modifiers: `on error` on 112 lines in 90 files (13 in os/), cache on 8 test goals, timeout on 2.

**Tests on the wiring:** 13 C# files, ~159 lines (`EventCollectionTests` 29 bindings, `EventCacheInvalidationTests`,
`EventHandlerTests`, `Stage8_ChannelEventsTests`, `AfterActionPayloadTests`, `SecurityFixTests`, …); the modifier tests
(`PLang.Tests/Modules/App/Modules/modifier/*`: fold, registry, frame, error handle, cache wrap, timeout after) and six
plang tests in `Tests/Modules/Modifiers/`.

## 2. Where each piece lands

| today | stage 8 |
|---|---|
| `Trigger` + `event/list` + `lifecycle` + `moment` + `LifecycleFor` + `_eventContainers` + `module/Events.cs` + `GetEventBindings` + `App.Event` + `channel/event` | gone. Each item has `on` (the shared empty one until its first binding); `app/event/on/<verb>.cs` event classes hold `before`/`after` binding lists; the running event is `this` (`%!event%`), the item it fired for `%!event.item%` |
| Before/AfterGoal, Before/AfterStep, Before/AfterAction | `on.start.before/after` on goal, step, action |
| goal/step Load triggers (dead) | `on.load` — new behaviour, see Q6 |
| Before/AfterWrite/Read, OnAsk | channel's `on.write`, `on.read`, `on.ask` |
| OnVariableChange (dead), the store's C# `OnSet/OnCreate/OnRemove` | variable's `on.set`, `on.remove` (+ `on.create`?) — see Q4 |
| OnCacheHit/Miss (dead) | cache's `on.hit`, `on.miss` (fired by `on.cache`) |
| OnError (dead) | `on.error` outcome — the modifier becomes it |
| — | type's `on.create` at `type.Create` (stage 9 moves births onto it) |
| `event.on(Trigger, …patterns…)` | `on.event(item, when, event, action)`: one line, `Item.on[Event][When].Add(action, context)` |
| `event.remove(EventId)` | removal by the binding's handle (Q7) |
| `event.skipAction(Value)`, `EventOverride`, `Handled` override | `cancel %!event%` (a before-handler cancels; its return is the result) |
| `mock.intercept` (BeforeAction + `EventOverride` + tag), `mock.reset`, `EventBindingId` | `mock` binds on the module's `on.start.before` with its filter, born cancelling, actor-scoped; reset removes the actor's mock bindings; no mock object, no id |
| `on.error` / `cache.wrap` / `timeout.after` modifiers, `modifier.list`, `Wrap`, `Catch`, `IModifier`, `ICatch`, `ModifierAttribute`, `action.Modifier`, `module.Modifier`, the `timeout` module | `on.error`, `on.cache`, `on.timeout` actions that bind on the action before them (program-scoped); the modifier concept dies |
| debug's 5 bindings | bindings on the goal/step/action types' `on.start` (every goal/step/action), filtered by the setting's goal/step |
| `test.Time` | the test binds `on.start.before/after` on its entry goal's steps (the step type's `on`, filtered to its goal) in its own App |
| `coverage.Watch` | binds `on.start.after` on the action type in the test's App; the per-modifier AfterAction goes, so coverage sees `on.*` actions as ordinary actions (they run) — see Q9 |
| `test.list.Made` (C# test hook) | stays a C# hook (not a plang verb) — or the App's own `on.create`; Q10 |
| builder: templates' `m.Modifier` walks, `Properties.llm` modifier text + rule 8, `decider.json` popular `on.error`/`cache.wrap`, python `formal.py` LAYER/attach, `prompt_c.py` modifier walk, `build_pr.py` | the `on.*` actions are ordinary actions: templates walk `m.Action` only; formal attaches nothing (an `on.*` action is written after its action and binds at run); python twins follow; one eval |

## 3. Slices (each its own commit, suites + builder check + consumer sweep; builder-visible ones ride one eval)

- **8a — the event classes and `on`.** `app/event/on/this.cs` (an item's `on`: the shared empty instance, never null;
  `this[event]`), the event base (`before`, `after`, `item`; `Add(binding, context)`, `Start(item, context)`), the verb
  classes needed now (`start`, `load`, `create`, `set`, `remove`, `write`, `read`, `ask`, `error`, `hit`, `miss`),
  a binding (handler, scope = the registering actor unless `app`, filter). Navigation: `%!app.goal["/start"].on.start.before%`.
  No consumer moves yet. C# tests for Add/Start/scope/cancel.
- **8b — goal, step, action start.** They fire `on.start` (their own and their type's), replacing `LifecycleFor`;
  cancel replaces `EventOverride`/`Handled`. Move debug's 5, `test.Time`, `coverage.Watch`; `event.on`/`remove`/
  `skipAction` stay as thin adapters until 8f so plang tests keep passing. Delete `LifecycleFor`, `_eventContainers`,
  `module/Events.cs`, `GetEventBindings`.
- **8c — channels.** `on.write`/`on.read`/`on.ask` on the channel; `channel/event` and `App.Event` go; the three-source
  merge becomes one list on the channel (a binding scoped to its actor, or `app`).
- **8d — variables.** `on.set`/`on.remove` on a variable (and the variable type), fired by the variable list; the C#
  store events move or stay (Q4).
- **8e — create.** `type.Create` fires `on.create` before/after (the door only).
- **8f — the `on` module (builder-visible).** `on.event(item, when, event, action)`; `event.on`/`remove`/`skipAction`
  and the `event` module go; `cancel %!event%`; mock rebinds (`mock.intercept` → a binding on the module's `on.start.
  before`). Teaching files; the 9 event `.goal` files rewritten; rebuild their `.pr`.
- **8g — modifiers become events (builder-visible).** `on.error`, `on.cache`, `on.timeout` bind on the action before
  them; the modifier machinery and the `timeout` module go; `.pr` format (Q5); the formal reader/writer; the builder's
  templates, `Properties.llm`, `decider.json`; the python twins; rebuild the 2 built `.pr` that hold a modifier, and
  the 90 `on error` / 8 cache / 2 timeout goals keep their text.
- **8h — the one eval** for 8f + 8g (golden + bootstrap, the 7f procedure), then the dead `Trigger` values that are
  new features (app start, error outcome, load) if ruled in (Q6).

## 4. Answers (decision 75, plang-cd; plang-visible ones go to Ingi)

- **Q5's timing, fixed:** a program-bound `on.*` binds when the program is **read** (the step's code), onto the action
  before it — not at run (in `[file.read, on.cache, on.timeout, on.error, variable.set]` file.read would already have
  started). The exact place (the action list's read, or the step's load) is 8g's trace; no pre-pass in the step runner
  (a *fork*). The rest of Q5 stands: ordinary siblings in `code`, `"modifier"` goes, `recovery` becomes on.error's
  `goal.call`, the 2 built `.pr` rebuilt.
1. Yes — a type's `on` fires for every item of the type, an item's own for that item: `%!app.type.step.on.start.before%`.
2. Yes — one filter on a binding (a predicate its creator writes); the pattern language goes.
3. Yes — one binding kind, one `Func`; `on.event`'s is `action.Start`.
4. Debug's watch → bindings. The call stack's diff: if it's the store's own bookkeeping, the store does it directly (no
   event); if it's an observer, it binds like debug — never a C# event beside `on.set`. **Trace which** (in 8d). Same
   sweep: `actor.setting.Written` → `app.Refresh` becomes an `on.set.after` binding (decision 51); OpenAi's two C# events
   (debug's LLM trace) become bindings on the llm actions' `on.start`.
5. Yes, with the timing fix.
6. Nothing stays dead: goal/step `on.load` and the app's `on.start` in 8b; error, variable, cache hit/miss in their slices.
7. A binding is an item; removing is its own verb, actor-scoped; ids go. The plang action must **not** be `on.remove`
   (under `on` an action's name is its event). Proposed name: **`on.unbind`** — no item has an `unbind` verb, so it can't
   read as an event. (To confirm.)
8. Order added; `Priority` and `StopOnError` go. A handler's error is the result's error: a failing before stops the
   action, a failing after makes the action's result that error — loud, a program error. The C# infrastructure bindings
   (debug, test, coverage) return no errors, so they never fail a program.
9. An `on.*` action is covered when the action it's bound on starts (it was in effect); whether its handler fired is the
   event's outcome.
10. `Made` stays a C# test hook.
11. A program-bound `on.*` fires for every actor; a runtime `on.event(item: …)` is scoped to its actor.

**Sequencing:** 8a now (the classes and `on`, no consumer moved, no behaviour change). 8b and later wait for 7f's eval —
8b changes how goals/steps/actions start, so the builder's own run changes, and a drop couldn't be traced otherwise.

## 5. The questions as asked

1. **"Each goal / step / action" bindings.** Today a pattern over every node (`before each goal`, debug's `*`). In the
   new model an item's own `on` fires for that item; I read "`app.type.text.on…` binds every text" as: a **type's `on`
   fires for every item of the type**. So "before each step" binds on the step type's `on.start` — step and action
   are types but not concept types (there's no `%!app.step%`), so it's `%!app.type.step.on.start.before%`. Right?
2. **Filters.** Today's patterns (goal name with `*`/prefix/regex, step Contains, `module.action`) — the new binding has
   "a mock's filter". Proposal: one filter shape on every binding (a small predicate the binding's creator writes:
   goal address, step index, action name, a mock's parameter match), not the old pattern language. The `event.on`
   `GoalPattern`/`StepPattern`/`ActionPattern`/`IsRegex` params go.
3. **C# handlers.** Debug, test and coverage bind C# code, not a plang action. Proposal: a binding holds one
   `Func<item, context, Task<data>>`; `on.event`'s is `action.Start` — one kind of binding, as today's `Handler`.
4. **Variable change.** The variable list's C# `OnSet/OnCreate/OnRemove` feed the call stack's snapshot diff and debug's
   watch (C# plumbing, per-store). Do they become `on.set`/`on.remove` bindings (one mechanism) or stay C# events beside
   the new plang-visible `on.set`? My pick: debug's watch moves to bindings; the call stack's diff stays a C# event
   (it's the store's own bookkeeping, not an observer).
5. **`.pr` shape of an `on.*` action.** Today `on.error` nests in the action's `"modifier"` array. Proposal: an `on.*`
   action is an ordinary action in the step's `code`, right after the action it binds on; at run, `Start` binds on the
   action before it once (the binding is part of the program). The `"modifier"` key goes; a `.pr` with it is
   `PrFormatOutdated` (2 built files to rebuild). `recovery` becomes `on.error`'s own action property (its `goal.call`).
6. **The dead triggers.** Load (never fired), app start, OnError, variable change, cache hit/miss are new behaviour.
   Which are in stage 8: the rule says every public verb has its event, so goal/step `Load` gets `on.load` for free
   once 8a exists; app `Start` too. I'd do them in their slices (load in 8b, app start in 8b) and leave nothing dead.
7. **Removing a binding.** `event.remove(EventId)` goes with ids. Proposal: `on.event` answers the binding itself (an
   item), and removing is the binding's own verb (`remove %binding%`), scoped to its actor. Mock's reset removes its
   actor's mock bindings by walking the module's `on`.
8. **Ordering and `stopOnError`.** Today `Priority` (debug uses `int.MaxValue` to go first) and `StopOnError=false`.
   Proposal: bindings run in the order added; a failing before-handler stops what it's before (its error is the result);
   a failing after-handler's error is written to the debug channel and doesn't fail the action. Debug/test/coverage
   need no priority. `Priority` goes.
9. **Coverage of `on.*` actions.** Today coverage counts a modifier through its own AfterAction (`action/this.cs:211`).
   After 8g an `on.error` action runs once (it binds); its handler fires only on an error. Coverage counts the
   `on.error` action as covered when it bound; whether the recovery ran is the error event's outcome. OK?
10. **`test.list.Made`.** A C# hook for tests to probe a test's App. The rule is about plang verbs; an App has `Start`
    (8b). Keep `Made` as the C# test hook, or make it the App's `on.create` (nothing creates an App through a type)?
    My pick: keep it; it's not plang-visible.
11. **Scope of program-bound `on.*` actions** (8g): the plan says they fire every time the action starts, unlike a
    runtime `on.event` (actor-scoped). The binding is on the action object itself (shared by every actor running the
    goal), so scope = every actor. Confirm.
