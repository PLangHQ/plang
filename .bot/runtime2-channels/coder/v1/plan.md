# Coder v1 plan — runtime2-channels

## Scope

Implement the architect's 9-stage refactor end-to-end. One stage at a time
in dependency order; only stop and ask Ingi if a question genuinely blocks
the work.

Per-stage rhythm:
1. Read the architect stage brief.
2. Read the test-designer stubs for that stage to understand the contract.
3. Implement.
4. `dotnet build PlangConsole`.
5. `dotnet run --project PLang.Tests` — stage's stubs go green, no
   regression in the rest.
6. `plang --test` (if the stage has PLang surface) — write/build the
   `.test.goal` bodies, stale count drops, fail stays 0.

## Order (matches architect's dependency chain)

1. Channel base abstract + Session/Message + Role + config (TimeSpan ISO 8601)
2. Stream concrete (refactor today's Channel into `Channel.Stream`)
3. Goal concrete + recursion rule + foundational set capture
4. `Write` Channel slot (IChannel) + source-gen resolution + builder catalog
5. `channel.set` / `.add` / `.remove` actions
6. PlangConsole entry-point wiring + `App.Run` invariant + drop `app.Channels`
7. Flat `App.Services` + `Service` type + drop `EscalationLevel`
8. Channel events (BeforeWrite/AfterWrite/BeforeRead/AfterRead/OnAsk)
9. `channel.migrate` API stub

## Things I'll watch for

- **Source generator changes** (Stage 4) touch `PLang.Generators` — must
  rebuild generators and downstream cleanly; cache key must still be stable.
- **`App.modules.output.Write`** is the canonical IChannel action — the
  current `Channels.WriteAsync(Write action)` overload in `App/Channels/this.cs`
  goes away. Need to confirm no other caller depends on it.
- **`App.Channels` drop** (Stage 6) — find every reference and migrate to
  `app.User.Channels` / `app.System.Channels` / `app.Serializers`.
- **`EscalationLevel` removal** (Stage 7) — architect says only the
  definition references it; I'll grep again before deleting.
- **PLang test goals** are non-deterministic per build; read the `.pr`
  after every `plang build` and verify module/action/parameters.

## Open assumptions I'm not stopping for

- Test-designer's "AfterWrite always fires (even when WriteCore throws)"
  call (`AfterWriteHandler_FiresWhenWriteCoreThrows`) versus architect's
  "After-handlers always fire, errors suppressed" — both consistent. I'll
  implement: AfterWrite fires after the operation regardless of throw,
  and a thrown handler is suppressed; the original outcome (success or
  error) is what the caller sees.
- `Channels.Resolve(null)` and `Resolve("")` both return the Output role
  channel — test-designer's call, plan-consistent.
- `channel.add` duplicate name → typed `DuplicateChannelName` Data error.
- Removing a role channel via `channel.remove` → typed
  `ChannelInvariantViolation` Data error.

If any of these turn out wrong mid-stage, I update them and note in the
session.

## Deliverables on done

- `summary.md` at coder root.
- All 9 stages' stubs green.
- `report.json` updated.
- One commit per stage on `runtime2-channels`, then push.
