# v3 — channel architecture cleanup

## Problem (high level)

The `Channel.Role` enum split channels into two implicit categories:
"role channels" (output/error/input — pre-registered, removal-refused,
backed by hardcoded boot wiring) and "custom channels" (user-registered
via `channel.add`, removable). That asymmetry leaks everywhere:

- `channel.add` vs `channel.set` — different verbs for what is
  semantically the same operation ("make this name resolve to that
  goal").
- `channel.remove` has a special case for role names.
- `Channels.Resolve(null)` falls back to the role enum.
- `IChannel.Resolve` walks role-named registry vs the broader registry.
- `EnsureRoleChannels()` boot invariant is keyed off the enum.

There is no *behaviour* that requires the split — only the convenience
of "output/error/input exist at boot so you don't have to declare
them." A `logger` channel could carry the same guarantees if a goal
declared it; the language shouldn't make that distinction first-class.

## Design

Channels are uniform. The names `"output"`, `"error"`, `"input"` are
just pre-registered names — not a separate concept.

- `Channel.Role.@this` enum — **deleted**.
- `Channel.@this.Role` property — **deleted**.
- No `IsDefault` / `IsCore` / `Direction` flags. A `Channel` is just
  `{ Name, backing, attributes }`.
- `Channels.@this.Defaults` — static readonly `["output", "error", "input"]`.
- `Channels.@this.Verify()` — replaces `EnsureRoleChannels()`. Confirms
  each name in `Defaults` is registered.
- `channel.remove` — refuses if `name` is in `Defaults`.
- `write` with no channel argument → resolves the name `"output"` (errors
  if it isn't registered).
- `channel.set` and `channel.add` collapse into one verb (kept name:
  `set`). Always upserts. `add` and `Add/DuplicateName` test go away.

## Subordinate fixes that fall out

These were the v2-debug findings; the cleanup absorbs them.

1. **`channel.set.Goal` typed as `Data<GoalCall>`** (was
   `Data<Variable>`). Builder pre-resolves PrPath at build time. Runtime
   loads from PrPath. Same pattern as `goal.call`, `event.on`,
   `http.request.OnStream`, `app.run`. Drops the `app.Goals.Get(name)`
   shortcut that was failing on un-registered helper goals.
2. **`channel.set.Actor` typed as `Data<Actor.@this>?`** (was
   `Data<Variable>?`). Mirrors `goal.call.Actor`. The `[Choices]`
   validator landed in v2 will catch the LLM-hallucinated
   `"__actor__"` / `"Context.Actor"` values at build time, which
   currently slip through.
3. **`Channels.Resolve` returns `Data` with error** (was: throws
   `ChannelNotFoundException`). The exception class is deleted. The
   source-gen-emitted IChannel slot pattern adopts the early-return
   shape that other `Data`-returning calls already use. This makes the
   `- on error key:"ChannelNotFound"` test path match.
4. **`Step.RunAsync` catch preserves typed exception keys.** Hygiene
   rule for cases where a real exception escapes (i.e. not a
   control-flow shortcut). When (3) is in place this stops being load
   bearing for the channel scenarios but remains the general pattern.

## Subordinate work — dynamic `[Choices]`

Separate but adjacent: extend `[Choices]` so the builder grows the
vocabulary as it walks steps. When `channel.set "logger" call MyGoal`
is processed, `"logger"` joins the channel-name choices for subsequent
write/remove/migrate actions in the same goal. Generalizes to other
declare-then-use patterns (variables, services).

Two shapes considered:

- **(a) BuildScope passed to validator.** Choices methods get a
  build-scope arg; declarations populate it. Concrete and clean but
  one more thing to thread.
- **(b) Action declares "I introduce names into scope X."** Attribute
  or hook on the action class. Catalog driver reads it uniformly.
  Bigger upfront, more declarative.

(b) matches `[Choices]`'s "the type owns its identity" instinct.
Decision deferred — pick after the channel cleanup lands so we have a
real consumer to design against rather than a hypothetical one.

## Tests

- C# baseline now: 2744/0/0. Must stay 2744+.
- PLang: 191 pass / 10 fail / 5 stale → expected after v3:
  - Stale: 4 (the four pre-existing Callback stales remain).
  - Fail: 0 expected for the channel scenarios. The 6 helper-goal
    failures resolve via the `Data<GoalCall>` typing. The 1
    `WriteToUnknownChannel` resolves via the resolve-returns-Data
    fix. The 2 actor-hallucination failures resolve via
    `Data<Actor.@this>?` + the `[Choices]` validator. The 1
    `Events/AddBeforeWrite` "Channel 'audit' not found" needs its
    own look — likely a missing setup step in the test body.
  - `Add/WithConfig` stale stays for now (LLM step-splitting on
    long modifier-chain lines — separate concern).
- Update test bodies to use `channel.set` everywhere `channel.add`
  appeared. Delete `Add/DuplicateName` (the rule it tests no longer
  exists).

## Out of scope for v3

- Dynamic `[Choices]` — design it after v3 lands so we're picking
  shape against a real consumer.
- The `Add/WithConfig` step-splitting fix.
