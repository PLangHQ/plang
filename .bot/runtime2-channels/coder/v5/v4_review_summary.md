# v4 review summary — codeanalyzer v1 verdict: FAIL

Six findings against the channels work landed in v3+v4.

| # | Where | Type | Disposition |
|---|---|---|---|
| F1 | `PLang/App/Services/this.cs:33-35` | Bug — `ConcurrentBag` Remove drains and rebuilds; concurrent `New()` falls into the gap, service silently dropped | Fix in v5 — switch to `ConcurrentDictionary<Guid, Service>` |
| F2 | `PLang/App/Channels/Channel/Goal/this.cs:59` | Bug — `ctx.Variables.Set("!data", data)` writes to actor-shared Variables; concurrent goal-channel writes race on the slot | Fix in v5 — route via `GoalCall.Parameters` AND introduce `Variables.Calls` AsyncLocal scope so the race goes away in fact, not just in API surface |
| F3 | `PLang/App/Channels/Channel/this.cs:244` | Latent crash — `Actor?.Context!` then `binding.Handler(ctx!, ...)` NREs when channel has no actor | Fix in v5 — guard, skip with diagnostic |
| F4 | `PLang/App/Channels/Channel/EventContext.cs` | Dead code — declared type never used in firing path | Delete in v5 — Ingi confirmed it's old/vestigial |
| F5 | `PLang/App/Channels/Channel/Stream/this.cs:157-167` | Silent contract violation — `ReadAllTextAsync`/`WriteTextAsync` hardcode UTF-8, ignore the `Encoding` property | Fix in v5 — resolve via `Encoding.GetEncoding(Encoding)` at call time |
| F6 | `PLang/App/modules/channel/set.cs:47-49` | Design gap — every goal channel stamped Output unless name is "input"; `GoalChannel` extends `Session` (Ask-capable) but is registered with `CanRead = false` | Fix in v5 — default goal channels to Bidirectional, keep input/output name shortcut, add explicit `Direction` parameter |

## Discussion notes (with Ingi)

- F2 architectural correctness alone (switching to `GoalCall.Parameters`) doesn't close the race — `App.RunGoalAsync(GoalCall, ...)` still injects parameters by writing to shared `context.Variables`. Ingi: "use Parameters, but it feels correct to really fix this."
- Real fix: per-call AsyncLocal parameter scope on `Variables`. Mirrors the existing `PushChannelsOverride` pattern (proven in this codebase), and the lifecycle/disposable shape of `CallStack.Push → Call.@this : IAsyncDisposable → RestoreCurrent`.
- **Naming:** `PushParameters` rejected as verb-shaped. Settled on `Variables.Calls.Push(parameters)` — `Calls` matches the existing `CallStack.Call` vocabulary, the discipline lives inside `Calls`, callers get a `using var _ = ...Push(...)` shape.
- **Why separate from `CallStack`:** `CallStack` is action-grained observability; parameter binding is goal-grained resolution. They have aligned lifecycles but different granularities and concerns. Goal-call action pushes one `CallStack.Call`; the same boundary pushes one `Variables.Calls.Push`. Each owns its own AsyncLocal stack.
- **Out of scope for v5 (logged in `Documentation/v0.2/todos.md` separately):** the wider question of how concurrent goal calls on the same actor share Variables for goal-body mutations (`set %counter%`). v5 fixes parameter passing only.
