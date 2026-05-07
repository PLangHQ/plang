# Codeanalyzer v2 review of coder v5 — what was flagged

| ID | Codeanalyzer's claim | Outcome in v6 |
|----|----------------------|---------------|
| B1 | `InvokeChannelHandler` logs a null-Actor diagnostic but falls through to `binding.Handler(ctx!, ...)` — handlers will NRE. | **Codeanalyzer's prescribed fix was wrong.** Stage 8 channel-event tests construct channels without an Actor and expect their handlers to fire. Returning `Data.Ok(null)` early breaks 10 tests. The correct fix is the v5 shape (log + forward null) — handlers that need ctx (notably `event.on`'s goal-dispatch handler) guard locally. Updated comment to make the contract explicit. |
| B2 | `RunGoalAsync(GoalCall)` injects params into actor-shared `Variables` → races on concurrent calls. | **Reframed by design discussion with Ingi.** Sequential `goal.call` is *not* a fork — it shares state with the caller (LoadUser pattern). The fork is whatever upstream operator spawned concurrency (parallel foreach iter, future async-call, listener accept-loop). That operator pushes the overlay; AsyncLocal carries it down. So `RunGoalAsync(GoalCall)` simply does not push, and `Variables.Set` becomes overlay-aware — writes land in `Calls.Current` if there is one, in the actor dict otherwise. Today no in-tree fork operator exists, so the actor-dict path is the only one used; the overlay path is exercised by the C# tests and is ready for parallel foreach when it lands. |
| L1 | "Inside a call frame, goal-body `set %x%` is invisible to subsequent reads" | **Dissolved.** Was an artefact of v5's read-only-overlay design. Under v6's "Push iff fork" rule, sequential `goal.call` has no overlay, `set %x%` writes to actor dict, `get %x%` reads it. The original L1 surprise can no longer happen. |
| I1 | `Push_FrameInvisibleToParallelFlows` uses `ContinueWith` (sequential), not actually parallel. | Fixed — converted to `Task.WhenAll`, renamed `Push_ParallelFlows_EachSeesOwnBinding`. Plus four more new tests covering the mutable-overlay semantics. |
| I2 | `Channel.Events` is `public List<>` with the recursion guard living in `Channel.@this` — split enforcement. | Encapsulated as `App.Channels.Channel.Events.@this` owning the bindings list, the lock, the `Match(type, name)` filter, and the AsyncLocal recursion guard (`IsActive`/`Enter`). Channel still orchestrates cross-source firing (per-channel + per-actor + app-level). Test surface preserved (`ch.Events.Add(...)`, `ch.Events.Count`). |
| F1, F4, F5, F6 | Closed in v5. | Still closed. |

## What changed conceptually

The v5 fix added `Variables.Calls` (an AsyncLocal scope) but pushed it at the wrong place — at the `goal.call` parameter-injection boundary. That manufactured fake isolation for plain sequential calls and produced L1.

v6 establishes the rule **Push iff fork**:

- `goal.call`, channel write, callback dispatch, sequential foreach iter — pass-through.
- Parallel foreach iter, future explicit async fork, future inbound listener accept — push a fork overlay.
- Inside a fork overlay, *every* `Set` is captured (not just shadowing) so siblings stay isolated.
- Reads cascade overlay → caller chain → actor dict.
- Communication out of a fork is via explicit return collection (`append to %results%`), not shared mutation.

This matches PLang's existing semantics where called goals leak their writes to the caller (LoadUser pattern, ProcessChunk-as-streaming-callback accumulator), and gives a clean isolation contract for parallel work when it arrives.
