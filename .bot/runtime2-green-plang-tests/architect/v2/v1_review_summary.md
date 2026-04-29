# Review summary — incoming feedback on architect v1

## From tester v2 (`tester/v2/result.md`, 2026-04-21)

Tester reviewed coder v1's implementation of my v1 waves. Seven findings total, three major (all for coder — W3 C# test gaps), three minor, and **one architect-owned critical finding**:

### F4c-1 (critical) — routed to architect

> "The five new prompt rules in `system/builder/llm/BuildGoal.llm` (arithmetic-on-set-RHS, download+save, wait/sleep, modifier shape, enum event types) are code-landed but have **zero observable effect on the test suite**."
>
> Coder's v1 summary says: "Rebuild regressed 38 previously-green tests ... Reverted all `.pr` changes. State returned to 122/35."
>
> Tests the rules were intended to unblock STILL FAIL:
> - `Loop.test.goal` (arithmetic on set RHS: `"0 + 1 + 1 + 1"` string) — fail
> - `ForeachDictionary.test.goal` — fail
> - `ConditionCompound.test.goal`, `ConditionCompoundAnd.test.goal` — fail
> - `SigningExpired.test.goal`, `SigningTimedOut.test.goal` — fail
>
> This is a known limitation coder flagged in the handoff — not a secret bug. But "Wave 4 done" overstates the state.

Tester proposed two options:
- (a) Accept W4c as landed-but-dormant; surgical per-goal rebuild with hand review.
- (b) Add PLang-level pipeline tests: compile specific .goal fixtures and assert the resulting `.pr` structure.

## Architect decision (with Ingi, 2026-04-21)

**Option (a) for near-term; option (b) deferred as a follow-up design task.**

Reasoning:
- LLM variance on cache miss is the likely cause of the 38-test full-rebuild regression, not bad rules.
- Surgical rebuild is bounded (~10 folders) with no blast radius — worst case a folder stays failing.
- Golden-eval infrastructure is the right long-term answer but needs its own session to stand up.

## v2 scope

Architect v2 = **surgical rebuild** for F4c-1. Plan in `v2/plan.md`.
