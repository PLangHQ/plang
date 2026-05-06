# architect — runtime2-channels

## 2026-05-06 — v1 plan complete; stages carved; test material ready

Plan, stages, and test files all in place. Ready to hand off to test-designer (then coder).

### What this branch does

Replaces the placeholder console-stream wiring inside the runtime with proper per-actor channel architecture: typed Channel base + Session/Message abstracts, concrete Stream + Goal channel types, per-channel config, role-based navigation, source-gen-resolved single-channel injection on actions, three module actions for managing channels, channel events, flat per-call Service collection, and a `channel.migrate` API stub.

### Plan structure (two-layer)

- **Spine**: `v1/plan.md` — narrative overview + stage index + key contract decisions inline.
- **Topic deep-dives** in `v1/plan/`:
  - `channel-events.md` — full design for Stage 8 (firing semantics, EventContext payload, recursion guard, OnAsk timing per channel kind, Service participation, test surface).
  - `channel-analysis.md` — what we have vs. what "standard" channel/streaming systems have vs. what only PLang can do.
  - `test-strategy.md` — narrative test strategy + 3 integration cuts.
  - `test-coverage.md` — per-stage coverage matrix + failure matrix + new-surface inventory.

Forward-looking ideas not in scope this branch:
- `Documentation/Runtime2/cool.md` — channel migration, smart-contract channels, counterfactual replay, sudo-for-I/O pattern, causal lineage (Data.Causes DAG).
- `Documentation/Runtime2/todos.md` — mobile signed code (`actions.run %code% level: 0`), `ExpiresInMs` migration to ISO 8601.

### Stage index

| Stage | File | Status |
|-------|------|--------|
| 1 | [Channel base + Session/Message + Role + Config](stage-1-channel-base.md) | pending |
| 2 | [Stream channel](stage-2-stream-channel.md) | pending |
| 3 | [Goal channel + recursion rule](stage-3-goal-channel.md) | pending |
| 4 | [Write Channel slot + IChannel + builder](stage-4-write-channel-slot.md) | pending |
| 5 | [channel.set / .add / .remove actions](stage-5-channel-actions.md) | pending |
| 6 | [Entry-point wiring (PlangConsole)](stage-6-entry-point-wiring.md) | pending |
| 7 | [Flat App.Services + Service type](stage-7-services.md) | pending |
| 8 | [Channel events](stage-8-channel-events.md) | pending |
| 9 | [channel.migrate (API stub)](stage-9-channel-migrate.md) | pending |

### Key design decisions (settled)

- **Service is not an Actor** — separate type at `App/Services/Service/this.cs`, lives in flat `App.Services` collection. Always System-signed.
- **No `app.Channels` shortcut** — redundant after `Serializers` moves to App. Callers reach via `app.User.Channels` or `app.System.Channels`.
- **`Channel` slot resolved by source-gen** — IChannel marker declares `Channel { get; set; }` of type `Channel.@this` (single resolved channel, not the registry). Source-gen resolves channel name from action JSON to a real Channel at setup.
- **Builder uses intent + per-actor channel inventory** — no `to <name>` pattern parsing. LLM picks from the inventory.
- **Channels named after roles**: `output`, `error`, `input` (was: `default`/`stdin`).
- **All three role-channels are runtime invariants** — entry points must register them; App.Run fails fast if missing. Missing custom channel produces typed `ChannelNotFound` error routed to error channel.
- **Goal channels under Session** — black-box goal call returns Data; behaviour from caller's perspective is Session-like.
- **Recursion rule** — Goal channels capture foundational channel set; their writes resolve against that, not the current overlay. Prevents infinite loops; gives fan-out via composition for free.
- **Channel events reuse existing event-binding pattern** — Channel.@this gains `Events` property; new EventTypes `BeforeWrite`/`AfterWrite`/`BeforeRead`/`AfterRead`/`OnAsk`; bindings filter by `ChannelName`. Before-handlers can abort by throw, cannot mutate. After-handlers always fire, errors suppressed.
- **Names** — `Mime` not `ContentType`; `Session`/`Message` for stateful/stateless abstracts; `Role` not `ChannelRole`. Two-uppercase rule applied throughout.
- **TimeSpan as ISO 8601 string in JSON** (`"PT30S"`) via custom JsonConverter using `XmlConvert.ToTimeSpan`. LLM never does math on time. Buffer stays `long` bytes.
- **EscalationLevel removed** from Actor (was dead code). Re-introduce inverted (`system=0, user=1, untrusted=100+`) when sandboxing actually needs it.

### Open threads

All resolved. (Threads A-E in the plan and review comments all closed.)

### Next step

Hand to test-designer using `plan/test-strategy.md` + `plan/test-coverage.md`. Then coder works through stages 1-9 in dependency order.
