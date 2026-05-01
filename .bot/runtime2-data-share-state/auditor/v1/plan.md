# auditor v1 — plan for runtime2-data-share-state

## Branch under audit

`runtime2-data-share-state`. Coder v1 + v2; codeanalyzer v1/v2/v3 PASS;
tester v1/v2 APPROVED. No security review on file. Branch is staged for
final auditor signoff before merge.

## Scope as I see it

The branch lands architect/v1's "every plang variable IS Data, cross-type
views are LIVE windows" model: Phase 1+2 (events→Lists, identity-preserving
`As<T>` / `AsCanonical`), Phase 3 (`Variables.Set` becomes dumb storage,
`Remove` fires `OnDelete`), Phase 4 (`variable.set` is the sole binding-mint
site with `MintTyped` + `CarryStateFromSource`), Phase 5a (foreach +
brought-back `.test.goal`s). v2 closes a follow-up bug (nested `%var%`
walk on plain `Data` + `JsonNode` dispatch).

Production scope vs `runtime2-generator-obp` merge:

| File | Lines | Notes |
|---|---|---|
| `PLang/App/Data/this.cs` | +326 | the foundation rewrite |
| `PLang/App/Variables/this.cs` | +26/-? | dumb storage + Remove fires OnDelete |
| `PLang/App/modules/variable/set.cs` | +98 | MintTyped + CarryStateFromSource |
| `PLang/App/modules/list/add.cs` | +18/-? | uses Data.SnapshotClone |
| `PLang/App/Utils/Json.cs` | +12 | Json.SnapshotClone options |
| `PLang/App/Utils/TypeConverter.cs` | +22 | JsonArray arm + JsonNode dispatch |
| `PLang/App/Debug/this.cs` | +10 | `+=` → `.Add()` syntactic |
| `PLang.Generators/Emission/Property/Data/this.cs` | +7 | plain-Data emission uses AsCanonical |
| `PLang/App/modules/loop/foreach.cs` | +17 | unchanged behavior, comment polish |

Prior reviewers covered:
- codeanalyzer/v1 — lifecycle audit (creation / unwrap / Data-in-Data /
  redundant copies). Found 4 cleanups. Closed in 60b8d1f3.
- codeanalyzer/v2 — verified the four fixes; quietly noticed the helper
  extraction unified divergent JSON-clone behavior (improvement).
- codeanalyzer/v3 — focused review of v2's nested-var walk + JsonNode
  dispatch. Pass 4 deep dive on the `set type=json` → LLM message path.
- tester/v1 — full suite, deletion tests, 7 findings (all coverage gaps,
  no false-greens on changed lines).
- tester/v2 — confirmed v3 codeanalyzer's predictive false-green flag is
  real (state-aliasing on AsCanonical container-walk transient is unpinned).

What no reviewer traced:
- **Cross-file contracts between the new dumb-storage `Variables.Set` and
  `App/Debug/this.cs`'s placeholder-subscribe pattern.** Architect raised
  it as an open question (architect/v1/plan.md:290) — "events are about
  the *name*; subscribers want to track that name across reassignments."
  Ingi flipped that to dumb storage. The flip changed observable
  behavior, but `Debug/this.cs` lines 141-160 was not updated to the new
  contract. The `+=` → `.Add(...)` syntactic change is all that landed.
- **No test exists** for the variable-watching debug feature
  (`--debug={"variables":[{"name":"x","event":"OnChange"}]}`) — the
  whole user-visible flow has no end-to-end coverage.

This is exactly the seam an auditor is supposed to look at: each piece
internally correct, the seam between them broken.

## What I'll do

1. **Read all prior reviews + report.json** (done).
2. **Verify dotnet test claims** (done — 2530/2539, the 9 are honest
   stubs). **Verify plang test claims** (done — 166/166).
3. **Trace the Debug ↔ Variables.Set seam** (done — confirmed regression).
4. **Review the rest of the diff for anything else the four other
   reviewers may have missed**, with a focus on cross-file contracts
   and architectural fit. Especially:
   - Does `CarryStateFromSource` make sense semantically? It carries
     events from the **source value** (e.g. y in `set %x% = %y%`),
     not from the prev binding. Is that right for OnChange/OnCreate/
     OnDelete subscriptions on the *target name*?
   - Are there other consumers of `OnChange`/`OnCreate`/`OnDelete`
     beyond Debug? (grepped — Debug is the only one.)
   - Is the `variable.set` MintTyped if-chain genuinely the sole binding-
     mint site? Other Set callers (`list.add` on the non-list-path
     conversion, `Action.cs:173` `__data__`, `cache/wrap.cs:37`) wrap
     into Data via `Variables.Set(string, object?, Type?)` — that path
     bypasses MintTyped. Acceptable scope or carryover?
5. **Write findings** to `.bot/runtime2-data-share-state/auditor-report.json`
   with severity, missed-by, and suggested fix.
6. **Write `verdict.json`** based on findings:
   - If Debug regression confirmed: **fail** with major.
   - Otherwise pass with minors.

## Out of scope

- Re-checking what codeanalyzer/v3 already passed on (the v2 nested-var
  walk + JsonNode dispatch). Spot-check only.
- The 9 honest-stub C# failures (Phase 5b/5c/6 deferred).
- The 43 sidelined `.test.goal2` files. Coder summary lists them as
  out-of-scope per Ingi.
- Cosmetic carryovers from tester/v1 (`global::` prefixes, `??`
  fallbacks). Logged as nits if appropriate.

## Risk if I don't pause for approval

I'll only be writing audit artifacts to `.bot/`. No production code is
touched. Final verdict drives the next bot (docs on pass, coder on fail),
not the merge itself. Low-risk to proceed.
