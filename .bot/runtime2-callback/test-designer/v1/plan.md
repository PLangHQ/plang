# Test Designer — v1 plan

Branch: `runtime2-callback`. Source of truth: architect's `plan.md` + `plan/test-strategy.md` + `plan/test-coverage.md` + four `stage-N-*.md` docs.

## Approach

The coverage matrix in `plan/test-coverage.md` is the spine. I'm not re-deriving it — I'm translating each row into a concrete test signature and naming it. Test-strategy adds two integration cuts that straddle layers; the matrix adds the per-`@this` and per-surface rows beneath. I'll add two narrow extras the matrix doesn't list explicitly (Stage-4 ask-user goal-not-found; Position chain-vs-frame distinction) where the stage docs imply them.

## Layout

- C# TUnit — `PLang.Tests/App/<area>/`. New folders: `Snapshot/`, `Callback/`, `Crypto/Encrypt/` etc.; existing folders get new files (`CallStackTests/`, `Errors/`, `VariablesTests/`, `Modules/signing/`, `Serializers/`).
- PLang `.goal` — `Tests/Callback/<scenario>/Start.goal` + `Start.test.goal`. New top-level `Tests/Callback/` folder; mirrors how `Tests/Signing/`, `Tests/Crypto/` are organised.
- All bodies stay stubbed — `Assert.Fail("Not implemented")` in C#, `- throw "not implemented"` in PLang. Nothing builds anything; this is the spec, not the code.

## Stage-tagging (added on top of architect's matrix)

I'll tag each test name with `[S1]`/`[S2]`/`[S3]`/`[S4]` so coder can see what should pass after each stage closes. Coverage matrix doesn't carry this column today; I'll add it inline in `test-plan.md`.

## Open questions for architect/coder before final write

These are the ambiguous coverage rows. I'll batch tests around them but mark each as "decision needed":

1. **`%!error.callback%` outside an error handler scope** — invalid (throw) or null? I'll write the row as "negative — throws" by default; coder/architect overrides if needed.
2. **Unregistered mimetype on a Channel** — error or fallback? I'll write as "negative — throws"; same caveat.
3. **`Error.@this.Callback` idempotency** — reference equality (cached `Data` instance) or value equality? Stage 4 doc says cached, so I'll write `IsSameReferenceAs`.
4. **`crypto.encrypt`/`crypto.decrypt`** matrix rows say "C# / goal" — I'll split each into two tests so both layers are explicit.
5. **`signature-rename.md` "compiles cleanly" row** — that's a build assertion, not a runtime test. I'll write a single `SignedDataTypeAlias_DoesNotResolve` C# test that fails if the old name comes back, and skip the second row.

I won't block on these — defaults above. If architect/coder disagrees during review I'll adjust.

## Batches (presented for approval, ~10 tests each)

| # | Area | Layer | Stage | Count |
|---|---|---|---|---|
| 1 | `ISnapshotted` interface + `Snapshot.@this` round-trip per-`@this` | C# | S1 | ~10 |
| 2 | `App.Providers` two-step Restore + referent-integrity hard errors | C# | S1 | ~6 |
| 3 | `Call.@this` Capture/Restore + Goal-stub + hash-mismatch | C# | S2 | ~8 |
| 4 | `App.CallStack.@this` Capture/Restore + `EventsSince` + `BottomFrame` | C# | S2 | ~6 |
| 5 | `App.Variables.SnapshotAt(error)` + diff reverse-apply + `Flags.Diff` auto-flip | C# | S2 | ~7 |
| 6 | `Data.@this.Signature` lazy property + caching + Context wiring | C# | S3 | ~7 |
| 7 | `JsonSerializer` + `PlangDataSerializer` round-trip + lazy expiry-for-ICallback | C# | S3 | ~8 |
| 8 | Channels routing by mimetype + `application/plang+data` registration + `SignedData` rename | C# | S3 | ~5 |
| 9 | `ICallback` + `AskCallback`/`ErrorCallback` records + Position semantics + Run | C# | S4 | ~10 |
| 10 | `Error.@this.Callback` lazy materialization + idempotency + `app.Callback.Signature` config | C# | S4 | ~6 |
| 11 | `callback.run` action handler + `signing.verify` gate + `crypto.encrypt`/`decrypt` v1 | C# | S4 | ~7 |
| 12 | PLang surfaces: `%!error.callback%`, `- run %callback%`, `- ask vars:`, builder validation | goal | S4 | ~8 |
| 13 | Failure matrix (negative paths consolidated) — tampered/expired/hash-mismatch/missing-goal/missing-provider/missing-identity | C# + goal | S2-S4 | ~10 |
| 14 | Integration cuts — in-process resume + durability round-trip | goal + C# helpers | S4 | 2 |

Approx 100 tests total. Distribution: ~85 C# TUnit, ~12 PLang goal tests, 2 integration goal-tests with helpers.

## Workflow

1. Present batch 1 → wait for approval → adjust → batch 2 → ...
2. After all 14 batches approved, write the test files in one pass. C# files go straight into `PLang.Tests/App/...`; goal files into `Tests/Callback/...`.
3. Write `test-plan.md` (the stable record), `verdict.json` (`{ "pass": true }`), update `summary.md`.
4. Commit + push.

## Not blocked

The five open questions above have defaults; I'll proceed. If any flip during review, the affected tests are local edits, not redesign.
