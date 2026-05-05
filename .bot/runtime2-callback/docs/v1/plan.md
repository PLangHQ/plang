# docs v1 — runtime2-callback

## Reading order
- coder summary v5 + handoff.md (final state, decisions list)
- security v1 + auditor v2 verdicts (S-F1/F3/F4/N3 closed; S-F2 deferred per Ingi)
- 4 character/CLAUDE.md proposals
- New files: `PLang/App/{Callback,CallStack,Snapshot,Channels/Serializers}/`, `PLang/App/modules/{callback/run,output/ask}.cs`, plus `*.Snapshot.cs` siblings.

## XML doc state — assessed
Spot-checked the new public surface (Callback/this.cs, ICallback, AskCallback, ErrorCallback, Signature/this.cs, Snapshot/{ISnapshotted,this}.cs, RestoredFrame, modules/callback/run.cs, modules/output/ask.cs, PlangDataSerializer, this.Snapshot.cs partials). Coder XML doc quality is **already high**. No gap-fills planned at the C# member layer beyond a once-over for any naked public surface I find while writing the architecture docs.

## Architecture docs (Documentation/v0.2/) — gaps to fill
1. **`callbacks.md` — NEW.** Two ICallback impls, the wire shape choice (slim vs full snapshot), the lazy signing gate on `data.Signature` for ICallback values, the EnsureSigned-then-verify discipline in `callback.run` (S-F1), the size caps + sensitive-property strip (S-F3/F4), the `%!ask.answer%` resume sentinel, position semantics. WHY ErrorCallback's wire is narrower than its in-process Snapshot fidelity. Doubles as the architecture doc + the developer reference for anyone touching callback.run / serializers.
2. **`snapshots.md` — NEW.** ISnapshotted as the type-system classifier (snapshot vs reconstruct buckets). Section tree shape. Per-subsystem wire shapes (CallStack, Variables incl SnapshotAt, Errors.Trail, Providers/Statics/Test/Build). Restore lifecycle + referent-integrity guarantees (no silent fallback). Why entries are by-name not by-ref.
3. **`good_to_know.md`** — append three entries:
   - `data.Signature` lazy population is **ICallback-only** carve-out (the architect-coder Q in handoff #2). Future devs grepping for "lazy" need to know plain `Data` does NOT auto-populate.
   - `RestoredFrame` is a **surrogate**, not a `Call.@this` — no Push, no AsyncLocal, no Stopwatch (handoff #4).
   - `Errors.Push` sets `error.App = this.App` so `Error.Callback` can materialise via `app.Snapshot()` (handoff #5).
4. **`architecture.md`** — verify it mentions the callback subsystem; add a one-paragraph pointer to `callbacks.md` if missing.

## User-facing docs (docs/modules/) — gaps
1. **`callback.md` — NEW.** User-facing reference for `- run %callback%`. Worked example: receive a callback envelope, run it, surface the resumed Data. Mention the seal-then-verify guarantee in plain terms ("PLang signs and verifies for you — tampered or unsigned envelopes never dispatch").
2. **`output.md`** — read it; if it doesn't cover the new ask shape, add the `vars:` annotation + the `write to %x%` resume mechanism. Note: this is a tester deliverable in the strict sense (PLang examples are tester turf), so I'll **flag-and-fill the prose** but if any `.goal` example I write would duplicate tester output I'll keep it minimal and link to `Tests/Callback/AskWithVars/Start.test.goal`.
3. **`index.md`** — add `callback` to the module reference table if missing.

## CHANGELOG entry
Lives in `v1/result.md`. User-visible additions: `callback.run`, `output.ask` semantics (vars+resume), `application/plang+data` mimetype, signing rename (`SignedData` → `Signature`), Snapshot/Restore on App.

## Proposal decisions
| # | From | Target | Decision | Why |
|---|---|---|---|---|
| 1 | architect v2 | `characters/architect/character.md` | **apply** | Review-server tooling is real, has API surface; without it future architect sessions edit comments.json by hand. Canonical workflow. |
| 2 | coder v4 | `characters/coder/character.md` | **apply** | User explicitly flagged stale .test.goal stubs as a real failure mode on this branch. Concrete rule, not generic advice. |
| 3 | codeanalyzer v1 | `characters/codeanalyzer/character.md` | **apply** | User explicitly flagged the "auditor changed code" incident. The fix is one tight scope rule. Reviewer-bot exception applies. |
| 4 | architect v3 | `characters/architect/character.md` | **apply** | The two-file convention was validated on this branch (test-designer pulled, asked for the matrix; architect added test-coverage.md). Codifying prevents the next branch repeating the gap. |

All four are real branch-incident-driven and persistent (apply to all future work, not just this branch). All four edit `characters/*/character.md` (writable for docs).

## Output files
- `Documentation/v0.2/callbacks.md` (new)
- `Documentation/v0.2/snapshots.md` (new)
- `Documentation/v0.2/good_to_know.md` (append)
- `docs/modules/callback.md` (new)
- `docs/modules/index.md` (update if needed)
- `docs/modules/output.md` (update — vars+resume)
- `characters/architect/character.md` (apply 2 proposals)
- `characters/coder/character.md` (apply 1 proposal)
- `characters/codeanalyzer/character.md` (apply 1 proposal)
- `.bot/runtime2-callback/docs/v1/result.md` (CHANGELOG)
- `.bot/runtime2-callback/docs/v1/summary.md`
- `.bot/runtime2-callback/docs/summary.md` (root)
- `.bot/runtime2-callback/docs-report.json`
- `.bot/runtime2-callback/docs/v1/verdict.json`

## Verdict expectation
**pass** — auditor v2 + security v1 already passed; the only remaining work is documentation. No code changes from docs.
