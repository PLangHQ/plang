# Security plan — builder-ergonomics v1

## Scope

Net delta vs `origin/runtime2`:
- **Channels** — `FreezeFoundational` / `FoundationalChannels` / `PushChannelsOverride` /
  `AppChannels.Snapshot` *deleted*; replaced by per-channel `AsyncLocal<bool> _executing`
  on `channel.goal.@this`, observed by `AppChannels.Get` which returns `null` for an
  executing goal-channel.
- **Tagged.cs** — removed internal `ClearCacheForTests` / `CacheSize`.
- **Conversion.cs** — primitive-conversion failure now captures `Exception` and chains
  into the inbound `errors.Error.ErrorChain` when the source value is itself an Error.
- **Builder pipeline** — LLM prompt changes, BuilderChannel goal, EmitBuildEvent goal,
  confidence-per-step. Orchestration only.
- **Tests** — new `Tests/Channels/GoalChannelRecursion/`; Stage3/Stage6 channel test
  rewrite; NormalizeTreeShape adjusted to per-key identity.

## Threat-model angles

1. **Recursion guard correctness.** Does `_executing` propagate across the await of
   `RunGoalAsync`? Does Task.Run / parallel inside a goal-body defeat it? Does the
   finally restore prev even on managed exceptions? Are cycles A→B→A contained?
2. **Bypass surface.** Does any other path in `AppChannels` resolve a channel without
   going through `Get`?
3. **Deletions.** Are there stale callers of the removed `FreezeFoundational` /
   `Snapshot` / `Push…Override` APIs? (Including reflection-driven tests.)
4. **Error-chain.** Does the new chain expose anything sensitive or create a cycle?
5. **Baseline rules.** Semgrep clean against new code (only baseline serializer-hygiene
   hits allowed).

## Method

- Diff vs `origin/runtime2` for all touched `PLang/app/**` files.
- Grep for callers of removed APIs across source (PLang, PLang.Tests, PlangConsole).
- Walk `AppChannels` for every code path that yields a `channel.@this` to a writer:
  `Get`, `Channel`, `Resolve`, internal `GetChannel`.
- Run `scripts/semgrep-scan.sh`; compare count against the known-15 baseline.
- Verify the regression test arms the guard (tester v2 already mutation-confirmed).

No interactive flow expected — straight-through review.
