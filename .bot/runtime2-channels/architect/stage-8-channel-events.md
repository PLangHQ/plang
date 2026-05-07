# Stage 8: Channel events

**Goal:** Make channels first-class event-bindable objects. Same pattern as `Goal.Events` / `Step.Events` / `Action.Events` — bindings registered globally fire when channel operations run.

**Scope:** see `plan/channel-events.md` for the full design (firing semantics, EventContext payload, recursion guard, OnAsk timing, Service participation, test surface). Stage 8 implements that design.

**Deliverables (concrete from `plan/channel-events.md`):**

1. **`App.Events.EventType`** — add 5 values: `BeforeWrite`, `AfterWrite`, `BeforeRead`, `AfterRead`, `OnAsk`.

2. **`App.Events.Lifecycle.Bindings.Binding.@this`** — add `ChannelName: string?` filter alongside existing `goalName` / `stepText` / `module` / `actionName`. Bindings without `ChannelName` set don't match channel events.

3. **`App.modules.EventContext`** — add three properties:
   - `Channel: Channel.@this?` — the channel firing.
   - `Data: Data.@this?` — the Data being written/read; null for OnAsk.
   - `Ask: AskCallback?` — populated for OnAsk only.

4. **`App.Channels.Channel.@this`** — refactor.
   - Add `Events` property (same shape as `Goal.Events`, `Step.Events`).
   - Make `WriteAsync`, `ReadAsync`, `Ask` *sealed* wrappers that fire events around abstract `WriteCore`, `ReadCore`, `AskCore`.
   - Concrete subtypes (Stream, Goal) move their bodies to the Core methods (renames from Stage 1 if Stage 1 already used Core; otherwise this is a small refactor).
   - Wrapper structure (pseudocode):
     ```csharp
     public sealed async Task<Data.@this> WriteAsync(Data.@this data, CancellationToken ct = default)
     {
         var beforeCtx = new EventContext { Channel = this, Data = data, Phase = Before };
         await Events.Before.Run(beforeCtx);  // throws → caller sees abort
         try { return await WriteCore(data, ct); }
         finally
         {
             var afterCtx = new EventContext { Channel = this, Data = data, Phase = After };
             try { await Events.After.Run(afterCtx); }
             catch { /* suppress; original outcome stands */ }
         }
     }
     ```

5. **`App.Actor.Context.@this.GetEventBindings`** — extend the owner-switch to handle `Channel.@this` (uses `BeforeWrite`/`AfterWrite`/`BeforeRead`/`AfterRead`/`OnAsk`, filters by the channel's `Name`).

6. **`App.modules.events.add`** (or wherever existing event registration lives) — extend builder catalog to expose the new EventType values and the `channelName` filter. Builder catalog teaches the LLM the surface:
   ```
   - add before write on "logger" channel, call AuditGoal
   - add after write on "logger" channel, call MetricsGoal
   - add on ask on "input" channel, call CaptchaGoal
   ```

7. **Recursion guard** — verify the existing `Context._activeEventBindings` mechanism prevents loops when a Before-handler writes to the same channel. Should work without changes; add a test to pin the behaviour.

**Dependencies:** Stages 1, 2, 3. Channel base + concrete Stream + Goal must exist for events to wrap their operations.

## Design

Most of the design is in `plan/channel-events.md`. Key contracts repeated for the coder:

### Read-only Before, abort-by-throw

Before-handlers can inspect `%!event.data%` and `%!event.channel%`. They cannot mutate the in-flight Data. Mutation belongs in Goal channels (composition); events are for cross-cutting hooks.

To abort the operation, the handler throws (or returns Data.Error). The Core never runs. After-handlers don't fire. Caller sees the thrown error.

### After-handler errors are suppressed

After-handlers always fire (in `finally`). If an After-handler throws, the throw is suppressed — the original operation's result stands. Failures should be reported via the error channel asynchronously (out of scope this stage; just don't let the After-handler corrupt the operation's outcome).

### OnAsk fires differently per channel kind

- Session: post-answer (handler sees the resolved Data).
- Message: pre-serialise (handler sees the AskCallback before it's written to the wire).

Concrete subtypes implement `AskCore` accordingly; the wrapper around `Ask` calls `Events.OnAsk.Run` at the appropriate point. Session: after `AskCore` returns. Message: before `AskCore` is called (because Message's AskCore writes the callback then returns Suspend).

### Service channels participate

Bindings filter by channel name only (no actor filter this branch). A binding for `"input"` matches User's input channel AND Service's per-call input channel. Document this in tests.

### Registration order, first thrower wins

Multiple matching bindings fire in registration order. If A throws, B doesn't fire. Same as existing event behaviour for goal/step/action.
