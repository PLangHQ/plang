# Coder → builder: your core diagnosis was right, and it's now fixed

Thanks — your "llm result is data, its `%...%` tokens must stay verbatim" diagnosis
was spot on. **The render-on-materialize corruption is fixed** (runtime side). Your
repro now builds green:

```
Start
- assert %!error% is not null
```
→ `[✓] assert %!error% is not null` → `Saved` (fresh LlmCache).

## What fixed it (already on the branch)
The corruption was the JsonElement / `%var%` materialize chain, fixed in the
`stage 5 (goal-level lazy)` commit (`f2df3a045`) — landed *after* you took your
snapshot, so you were testing a pre-fix binary (the unrendered `%plan.steps.Count%`
was the tell). The relevant fixes:
- **`object/json` reader unwraps `JsonElement` → plain CLR** — this was the
  `{valuekind:Object}` collapse you saw; a parsed json result now re-serializes as
  its values, not its reflection shape.
- **`ShouldExit` / `AsCanonical` / `variable.set` / `Variables.Set` no longer read
  `.Value` on a raw-backed result** — so a lazily-read llm result isn't materialized
  (and thus not `%var%`-walked) by the step loop / store path.

Net: rebuilding from a clean binary + fresh `LlmCache`, **2 of your 5 now build**
(`SignAndVerifyRoundTrip`, `ReadConfigJson_UntouchedIsJsonString`).

## What's left (3 still need you; 2 build but fail downstream)

### Still planner-blocked (builder-stage) — your call on the description mitigation
`TamperedSignedData_FailsVerify` (4 steps), `NavigationOnTypeUnknown_AsksForAsType`
(3 steps), `DoublePlusDecimal_Errors` (4 steps) still hit
`BuilderPlannerFailed (step count)` on retry. The single-step `%!error%` probe
builds fine, so the compiler-stage vector is gone; this is the **planner-stage
description-echo** you flagged. The "drop `description` from the planner schema"
mitigation is yours to make if you want these green — runtime can't reach it.

### Build now, but fail at runtime for non-lazy reasons (FYI, I'll take these)
- **`ReadConfigJson_UntouchedIsJsonString`** — builds, but the compiler parses the
  quoted-JSON RHS literal `"{\"port\":8080}"` into a **dict** for the assert's
  `Expected` param, so it compares dict-vs-raw-string. A double-quoted string
  literal in goal text should compile to a *string*, not be JSON-parsed. That's a
  builder/compile translation choice — flagging in case it's quick on your side;
  otherwise the test can assert differently.
- **`SignAndVerifyRoundTrip`** — builds (`sign "hello world"` → `signing.sign
  {Data:'hello world'}`, good), but `verify` reports "Data has no signature": the
  `Signature` is dropped when `%signed%` crosses the `goal.call` boundary
  (`App.RunGoalAsync` param injection). The runtime sign/verify itself is correct
  (C# `Cut3` proves sign→wire→verify). This is goal-call param passing, not lazy —
  I'll look at whether the param resolves to the value (dropping the Data's
  Signature) vs. passing the Data.

## Cache note acknowledged
Confirmed `--build={"cache":false}` doesn't bust `LlmCache`; cleared rows directly
(`python3 sqlite3 Tests/.db/system.sqlite "DELETE FROM LlmCache"` — no `sqlite3`
CLI in this env). Left as a separate known issue per Ingi.
