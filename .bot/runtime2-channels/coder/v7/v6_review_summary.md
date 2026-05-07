## Codeanalyzer v3 review of coder v6

**Verdict:** FAIL — 1 bug, 1 latent trap, 1 low-priority issue.

### B1 — `Channel.Events._active` is `static`
Instance state declared static; recursion guard becomes per-flow across all channels instead of per-channel. Concrete failure shape only triggers when the same Binding instance lands on two channels (binding IDs are 8-char GUIDs, so cross-channel collision is otherwise unreachable). Still, the shape is wrong: a per-channel guard should hang off the instance.

**Fix:** drop `static` from `_active` declaration.

### L1 — `AsyncLocal<HashSet<string>>` mutates a shared reference
`AsyncLocal<T>` propagates the same reference into spawned child tasks. If a binding handler ever calls `Task.WhenAll(...)` while inside `Enter(...)`, both flows would mutate the same `HashSet<string>` and `Releaser.Dispose` would clear bindings the parent still cares about. No callsite triggers it today (`FireBefore`/`FireAfter` iterate sequentially), but the trap is structural.

**Fix:** make `Enter` copy-on-write — snapshot parent set, install a new set, restore parent on dispose. `Releaser` now holds a `parent` reference, not a single id.

### I1 — `Variables.Snapshot()` ignores overlay
Acknowledged, deferred. Only the Stage-9 migration stub calls it today; correct semantics depend on a design call ("scope-aware vs actor-shared snapshot") that isn't ours to make until a real caller lands.

### Decision agreed with Ingi
Fix B1 + L1, leave I1 noted.
