# 8g, modifiers become events: the architect's own trace, before the coder's

Read at 26a9d8222. The comparison goes at the end.

## What is there today

- **The action holds its modifiers and on.error's recovery** as `Modifier` (`modifier.list`, `action/this.cs:52`) and `Recovery` (an action list, `:67`), so they're nested in the `.pr` under the action. Formal already writes them as siblings (`file.read(…); on.error(…); cache.wrap(…)`), and the parser attaches each to the action before it.
- **The run:** `action.Start` → if there are modifiers, `Modifier.Wrap(DispatchAsync)` (`:204-216`). Then `action.on.start.after` fires once per modifier (coverage).
- **Composition (`modifier/list/this.cs`):** sorted by `[Modifier(Order)]` into layers: on.error 0 (outermost), cache.wrap 50, timeout.after 100 (innermost). Written order is kept within a layer. Adjacent `on.error` clauses are ONE try/catch, the first matching clause handles, and the others never see the error (`Catch`, `:70-92`).
- **on.error (`on/error.cs`):**
  - filters `StatusCode`/`Key`/`Message` (no filter matches everything);
  - `RetryCount`/`RetryOverMs` re-run `next`, the *inner* chain, so **each retry passes through cache and timeout again and gets its own deadline**;
  - `Order` is RetryFirst (the default) or GoalFirst (recovery, then retry);
  - `IgnoreError` is the last fallback;
  - on success it marks the live frame `Handled` (takes it out of `%!error%`);
  - recovery runs under a `DiffScope`, and a failed recovery chains into the original error's list.
- **cache.wrap (`cache/wrap.cs`):**
  - the key is rendered per run (a template), else the step's position (`CallStack.Step`);
  - a hit returns a copy and sets `!data` itself; a miss runs `next` and stores a success with its content materialized.
  - A recovery result is never cached, because recovery is outside the cache.
- **timeout.after:** a linked CTS with a deadline pushed on the context around `next`. A timeout is a failure that on.error, outside it, catches.
- **The builder:** the templates join `m.Action` with `m.Modifier` (`decider.state.template:47,72,83`, `properties.template:56,67,81,122`); `Properties.llm:45` teaches the postfix rule. So the prompts change, and that's 8h's eval.

## What the plan and decision 75 settle

- `on.error`, `on.cache` and `on.timeout` are `on` actions right after the action they bind on (the postfix rule stays), and they are ordinary siblings (75-5).
- **They bind when the step's code is read, not when it runs** (75, the timing fix), on the action's own `on`, and fire for every actor (75-11).
- A handler's error is the result's error (75-8). An `on.*` is covered when its action starts (75-9).
- Dies: `modifier.list`, `Wrap`, the catch grouping, `[Modifier(Order)]`, `IModifier`/`ICatch`, `ModifierAttribute`, `cache.wrap`, `timeout.after` (the timeout module goes; the cache keeps its provider), and `module.Modifier` (the templates' view).

## Behaviour that must survive (the tests)

1. The first matching `on.error` clause handles, in written order, and the others don't see the error.
2. A retry re-runs the real work. Today **each attempt has its own deadline and its own cache lookup**; decide whether that stays.
3. A timeout is an error that `on.error` catches, and "error outermost" holds with no order numbers.
4. A cache hit skips the work (and its deadline) and is `%!data%`; only a success of the real work is stored, **never a recovery's result**.
5. A handled error leaves `%!error%`: the frame is marked `Handled`. An ignored error stays in the audit.
6. GoalFirst vs RetryFirst ordering. A failed recovery chains into the original error.
7. Recovery runs under the diff scope (`SnapshotAt`).

## Open design points (for the coder's trace to answer)

- **The retry door:** the error binding must re-run the action's dispatch in the same frame. That's the action's knowledge (an internal member on the action), not the binding re-implementing dispatch. Does a retry re-fire the before-start bindings (timeout, cache)? Today it does, in effect.
- **Where the error outcome fires relative to after-start:** cache's store must see only the real work's success, and timeout's clear must not strip a retry's deadline.
- **Where the binding at code read lives:** the step's code list (`action.list`) reading its actions. Each `on.*` action binds itself onto the action before it (behaviour on the element). What does its `Start` do at run: nothing?
- **The `.pr` shape:** `Modifier` and `Recovery` nested under the action → siblings. Every `.pr` with a modifier gets rebuilt (no backward compat), and the reader and the formal writer change.
- **A sibling `on.*` started at run would overwrite `%!data%`** (`action.Start` writes its result to `!data`, `:222-223`), so a `variable.set(Value=%!data%)` after `file.read(…); on.cache(…)` would get the on-action's empty answer. What was bound at read must not run as a step action.
- **`cache.wrap` setting `!data` on a hit** (the rest of this point follows the comparison below)

## Comparison with the coder's trace (`.bot/app-systems/coder/v9/8g-trace.md`, 5e54c63ae; read after mine was pushed, 7fceb96aa)

- **Same:**
  - the crux (wrappers, not a before/after pair);
  - the mapping (cache and timeout on start's before/after, error as the outcome);
  - retry needs a door on the action;
  - the per-attempt-deadline question;
  - the builder templates.
- **The coder's adds:**
  - the concrete format keys (`"modifier"`/`"recovery"` at `action/this.Item.cs:66-85`, `serializer/Reader.cs:73-92`);
  - **the Python twins** (`tools/decider/{formal,prompt_c,build_pr,…}`), which I missed again (the lesson from 7f);
  - the C# test folders;
  - the two built `.pr` files that carry modifiers.
- **Mine adds:**
  - the behaviour table as pins;
  - the error outcome must fire after after-start (cache and timeout semantics);
  - a retry must re-run the whole attempt (shared deadlines break timeout retries);
  - **the `%!data%` clobber** if a bound `on.*` is started at run;
  - cache's hit state against a reset TTL.
- **Lesson:** my builder-side sweep again stopped at the templates. The Python twins are part of every builder-visible change.

The rest of the `cache.wrap` point: duplicates `action.Start`'s own `!data` write (`action/this.cs:222-223`). With a cancel, the answer is the result, and `Start` writes `!data` once.
