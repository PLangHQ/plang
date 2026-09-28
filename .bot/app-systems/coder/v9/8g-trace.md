# 8g — modifiers become events: trace (coder, at 937ca4162)

Plan: `on.error`, `on.cache`, `on.timeout` bind on the action before them; the modifier machinery and the `timeout` module
go; `"modifier"` leaves the `.pr`; the formal reader/writer, builder templates, `Properties.llm`, `decider.json`, the
python twins follow; the 2 built `.pr` with modifiers rebuilt; the ~91 `on error` / 9 cache / 2 timeout goals keep their
text. Decision 75-Q5: an `on.*` is an ordinary sibling in `code`, bound when the program is READ (not at run);
`recovery` becomes on.error's own action property.

## 1. What exists

**The machinery** — `goal/step/action/modifier/this.cs` (a modifier IS an action subtype: `Layer` from
`[Modifier(Order)]`, `Wrap`, `Catches`, `Handler`, `Recorded`); `modifier/list/this.cs` (sorted by Layer; `Wrap` folds
right-to-left, adjacent catching clauses grouped into ONE try/catch where the first non-null `Catch` wins);
`module/ModifierAttribute.cs`, `module/IModifier.cs` (`IModifier.Wrap(next)`, `ICatch.Catch(failed, attempt)`);
`module/this.cs:78-101` (mints the subtype; `Modifier` view); `action/this.cs:49-52` (`Modifier`), `:67` (`Recovery`,
a structural slot on every action, used only by on.error), `:204-215` (`Modifier.Wrap(() => DispatchAsync)` then one
action-type `after` per modifier for coverage).

**The three** — layered outermost first: `on.error` (Order 0) → `cache.wrap` (50) → `timeout.after` (100) → dispatch.
- `on.error` (`on/error.cs`): filters StatusCode/Key/Message; `RetryCount`/`RetryOverMs` re-run `next` (the INNER
  chain — cache+timeout+dispatch); `Order` GoalFirst/RetryFirst; recovery = `action.Recovery` run inside a DiffScope;
  `IgnoreError`; success marks `CallStack.Current.Handled` (takes the error out of `%!error%`).
- `cache.wrap` (`cache/wrap.cs`): key = `Key` or `step:{goalPath}:{index}`; hit → the cached value (and `%!data%`),
  dispatch skipped; miss → run, store on success.
- `timeout.after` (`timeout/after.cs`): a linked CTS pushed on the context around `next`; its own firing → `ServiceError`
  "Timeout" 408; a parent cancel rethrows.

**Formats** — JSON `"modifier"` + `"recovery"` keys (`action/this.Item.cs:66-85`, `serializer/Reader.cs:73-92`);
formal: an action's modifiers follow it after `;` and `Formal.Attach` hangs them on the preceding action
(`Formal.cs:165-191`, `Recovery=` only on on.error `:206-221`); the pick list prefills them (`pick/list/this.cs:332-343`).
Built `.pr` with `"modifier"`: `os/system/error/.build/show.pr`, `os/system/builder/BuildGoal/.build/start.pr` (4) —
the builder's own goal. 4 old `"modifiers": []` `.pr` in `os/` (dead/old-format).

**Builder** — `Properties.llm:45-47,63,70,82-83` (the modifier rule + rule 8); `properties.template:47-122` and
`decider.state.template:47-83` (`m.Action | concat: m.Modifier`); `decider.json:15-31` (on.error, cache.wrap).
**Python** — `tools/decider/{formal,prompt_c,build_pr,child_eval,formal_check,formal_fixture,params,harness,
pr_bootstrap}.py` (LAYER/attach/takes_recovery/is_modifier/"modifier"+"recovery" keys).
**Tests** — C#: `PLang.Tests/Modules/App/Modules/modifier/*` (fold, registry, frame, cache wrap, timeout after, error
in play, error handle), FormalReaderTests, Generator Matrix Modifier/Snapshot; plang: `Tests/Modules/Modifiers/*` (6),
`Tests/Errors/*` (on error), `Tests/App/CallStack/TimeoutProducesCancellation`.

## 2. The shape question — a wrapper is not a before/after pair

The three are *around* the dispatch, not beside it:
- **retry** re-runs the action (the inner chain) from inside the failure handling;
- **a cache hit** skips the dispatch and IS the result; a miss stores after;
- **a timeout** spans the dispatch: its CTS is pushed before and popped after, and "did MY cts fire" is per firing.

How each maps onto events (my proposal — `on.*` binds on the program action's own events, program-scoped):

| | binds | does |
|---|---|---|
| `on.cache` | `start.before` + `start.after` | before: a hit cancels with the cached value (`Handled`); after: a success stores |
| `on.timeout` | `start.before` + `start.after` | before: push a linked CTS; after: pop it, a fire of its own → the Timeout error |
| `on.error` | the action's **`error`** outcome event | filters; recover (its `Recovery` property); retry — needs a door to re-dispatch |

The questions this raises:

1. **Per-firing state across before/after** (timeout's CTS, cache's "did I hit"): hold it on the action's frame, which
   spans before → dispatch → after (decision 132), not on the shared binding. A typed slot on `call.@this` per kind, or
   one "the bindings' own" slot? (The `_items` bag is the smell we avoid.)
2. **Retry re-runs what?** Today `next` = cache + timeout + dispatch. As events, retry = the action's dispatch again
   (its own `Start` minus its own before/after? or its dispatch only?). I'd add the door on the action:
   `action.Retry(context)` → the dispatch again inside the same frame (the frame already survives a retry —
   `action/this.cs:233` says so). Does a retry re-fire `on.timeout`'s before (a fresh deadline per attempt, as today)?
   Today yes (timeout is inside error). As events it wouldn't unless retry re-fires start.before.
3. **Order between them** — today fixed by Layer (error outermost). As bindings: before-side runs in order ADDED; the
   step's text order is not the layer order (`read x, timeout 100ms, on error …, cache 5m`). Keep the layer as the
   binding's own position (each `on.*` kind declares where it sits), or accept text order?
4. **The `error` event** — where does it fire, and its fold? Several `on error` clauses: first match that handles
   wins (today). An error-event binding list whose fold is "first Handled answer stops" is the before-fold; the
   after-fold runs all. Which side do `on.error`s bind on, and does the event fire BEFORE the action's
   `start.after` (so after-bindings — coverage, debug — see the recovered result)?
5. **Binding at read** — the `.pr` reader, on reading an `on.*` sibling in `code`, binds it on the preceding action
   (program-scoped, every actor). At run the `on.*` action is then a no-op in the step loop. The formal reader
   (`Formal.Attach`) does the same at build. Where does "an `on.*` binds on the action before it" live — the action
   list's `Add` (the list knows "before it"), or the `on.*` action's own `Bind(previous)`?
6. **`recovery`** — becomes on.error's `Recovery: list<action>` property (not the structural slot on every action);
   `Recovery` and `internal set Step` on action go.
7. **Names** — `cache.wrap` → `on.cache`, `timeout.after` → `on.timeout` (plan); the `timeout` module goes; `cache`
   keeps its other actions. Parameter names stay (`DurationMs`, `Sliding`, `Key`; `Ms`).
8. **Coverage** — the per-modifier after goes; an `on.*` is covered when the action it's bound on starts (75-Q9).

## 3. Order of work (after the rulings)
1. The frame slot(s) + `action` door for retry; the `error` event firing in `action.Start`.
2. `on.cache` / `on.timeout` / `on.error` as binding kinds (like `binding.mock`, `binding.action`), their actions binding
   on the preceding action; delete `IModifier`/`ICatch`/`ModifierAttribute`/`modifier.*`/`Recovery`/the timeout module.
3. `.pr`: reader binds `on.*` siblings at read; `"modifier"`/`"recovery"` keys → `PrFormatOutdated`; formal reader/
   writer; pick-list prefill.
4. Builder templates, `Properties.llm`, `decider.json`, teaching `.md`; python twins.
5. Tests; rebuild `show.pr` + `BuildGoal/start.pr` (the builder's own — builder check after), the Modifiers/Errors plang
   tests; 8h's eval measures the prompt.
