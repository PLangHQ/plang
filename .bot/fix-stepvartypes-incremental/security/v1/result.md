# Security v1 — Result

**Verdict: PASS.** No critical/high open findings.

## Attack-surface map (delta only)

| Area | Change | Trust boundary | Mitigation | Gap |
| --- | --- | --- | --- | --- |
| `path.@this` display form | `_relative` now `/`-anchored, fwd-slash normalized | unchanged — `_absolutePath` still goes through `PathHelper.GetFullPath` in ctor | AuthGate fence intact | none |
| JSON contextual converters on write | `Conversion.cs` uses `ContextualReadOptions` for both serialize and deserialize | symmetry restores path round-trip | `PathJsonConverter` is pure (path↔string) | none |
| `results.json` payload | adds `output`, `timings`, switches to `ReportOptions { IgnoreCycles }` | results.json is on-disk test artefact; same trust level as the user's source | `IgnoreCycles` breaks the cycle that previously crashed serialization | broadens the standing Medium on `Variables.Snapshot()` leakage to also include `Output` (captured stdout). Tracked, see below. |
| child-App rooting in `test/run` | `new app.@this(parentApp.AbsolutePath)` (was `test.Directory`) | parent and child share trust root by design | corrects PrPath resolution under canonical paths | none |
| Event-binding capture (`outputBuf`, `stepStarts`) | non-locked `StringBuilder` / `Dictionary<int,long>` reachable from `BeforeWrite`/`BeforeStep`/`AfterStep` | scoped to one child App | PLang step execution is sequential; channel `WriteAsync` serializes per-channel | informational — see "Concurrency note" below |
| OpenAi cached-tokens / cost math | pricing table + cost arithmetic | no IO, pure data plumbing | bounded by `usage` JSON shape | none |

## Findings

None gating.

### Note 1 — `Output` field added to results.json (advisory)
**Severity: informational** (extends the existing standing-Medium "Variables.Snapshot() in test module doesn't honor `[Sensitive]`" — same artefact, same threat model). The child test's full stdout is now captured via `BeforeWrite` and serialized into `results.json` under `runs[].output`. A test that writes a secret with `output.write %secret%` will land that secret in the test artefact. Compare with the standing finding on `AssertionError.Variables` — both feed the same on-disk file.

Why not raise to Medium independently: this is *user-elected* output (the test author chose to write it), the destination file lives at the same trust level as the source goal, and every test framework captures stdout this way. The same redaction fix that solves the Variables case (e.g. `[Sensitive]` honoring at snapshot/serialize time, plus a configurable redactor for channel output) will solve this one. Document this as the second instance under the existing finding rather than spawn a new track.

### Note 2 — Event-handler concurrency (informational)
`outputBuf` (`StringBuilder`) and `stepStarts` (`Dictionary<int,long>`) are accessed without locks. The `Timings.cs` doc-comment correctly observes "scoped to a single Run (one thread of execution within its child App), so no concurrency surface is needed". This holds as long as:
- PLang goal execution remains sequential within a single child App.
- Channel `WriteAsync` continues to serialize per-channel (it does — `WriteAsync` is a single sequential `try { await WriteCore } finally { fire AfterWrite }`).

If a future change introduces concurrent writes from a goal (e.g. parallel sub-tasks) or concurrent step execution within one goal, both fields become race-y. Outcome of a race is **garbled captured output / wrong timing**, not a security incident — but worth a one-line guard if and when the concurrency model changes.

## Standing findings — status check
- **Variables.Snapshot() leakage / Image OOM / Conversation continuity / Fluid MaxSteps / Data.Clone shallow / Call.Diffs raw / CallStack.Flags / callback.run signing gate / Channel.Stream cap / MigrationEnvelope keyless "Signature"** — all unchanged on this branch.
- **`path.@this.IsUnder` dotdot-bypass** — confirmed still CLOSED. The ctor canonicalization (`PathHelper.GetFullPath`) is the gate and is untouched.

## Verdict
PASS.

```
VERDICT: PASS
Next: run.ps1 auditor stepvartypes-incremental "Review the code on branch fix-stepvartypes-incremental" -b fix-stepvartypes-incremental
```
