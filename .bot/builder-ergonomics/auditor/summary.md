# Auditor summary — builder-ergonomics

**Version:** v1
**Verdict:** PASS (2 minor, no critical/major)

## What this is

`builder-ergonomics` shipped a 7-priority response to coder's
user-feedback report on builder friction: per-channel `IsExecuting`
recursion guard (replacing the foundational-snapshot mechanism), P4
root-cause-first error chaining in `Conversion.cs`, P6 confidence-per-step,
builder output routed through a named `"builder"` goal-channel, planner
verb rule + Actor-from-step discipline, and `list<T>` schema
canonicalisation. Tester v2 PASS (mutation-confirmed channel guard
coverage), security v1 PASS (1 Low, latent).

## What I cross-traced

Auditor's unique value is end-to-end tracing across file boundaries
(channels guard: 3 sites all line up; P4 conversion fix: source +
test + caller scenario), plus the OBP-smell discipline. Data-normalize
commits riding via the runtime2 merge were already audited
(`1fadeb67b`); this pass focuses on what is **new on
`builder-ergonomics` since the merge**.

- Channel `IsExecuting` guard: `goal/this.cs:InvokeGoal` (arm + finally) ↔
  `channels/this.cs:Get` (consult) ↔ `Tests/Channels/GoalChannelRecursion/`
  (real-InvokeGoal exercise, `.pr` actions match step text, tester
  mutation-confirmed). Sound.
- P4 root-cause: `Conversion.cs:401-414` — Error-source path appends
  conversion failure to `sourceErr.ErrorChain` and returns the source.
  `ErrorBuryingReproTest` asserts key ordering *and* `Format()` byte
  offset ordering. Robust.
- Tagged cache test: `Tagged.ClearCacheForTests`/`CacheSize` removed,
  `DebugModeBypassTests` call site gone, new test asserts
  `ReferenceEquals(PropertiesFor,...)`. Per-key identity, no parallel
  race. Clean.
- Planner verb rule + Actor-from-step: codifies hand-patches in
  `Plan.llm` and `goal.call.notes.md`. `list<T>` schemas aligned with
  `Compile.llm:235`.

## Findings

- **A1 (Minor, concur with security F1).** `AppChannels.Channel(string)`
  bypasses `IsExecuting`. Today only `file/read.cs:76` reaches there
  (for builder warnings) and the builder channel body writes to
  `output`, not back to `"builder"` — no recursion path closes. Latent.
  Two-line fix mirrors the `Get`-side guard. Not a blocker.
- **A2 (Minor).** `Conversion.TryConvertTo` mutates the caller's
  `Error.ErrorChain` as a side effect of a failed primitive conversion.
  Correct behaviour for the demoed scenario; the smell is that an
  input-mutating side effect is hidden behind a conversion. Note for a
  future cleanup pass.

## Not-findings (checked, clean)

- Removed-API stragglers (`FreezeFoundational`, `FoundationalChannels`,
  `PushChannelsOverride`, `Snapshot`) — grep clean.
- `Channel(name)` callers — exactly one (`file/read.cs:76`).
- GoalChannelRecursion `.pr` actions match step text (no false-green
  like the deleted `UnknownVerb`).
- No new System.IO or `Console.*` violations on branch-local commits.

## Next

Branch is clean. Next: docs.

VERDICT: PASS
Next: run.ps1 docs builder-ergonomics "Review the code on branch builder-ergonomics" -b builder-ergonomics
