# Builder: 4 LazyDeserialize goal tests need you

The runtime/lazy side is done (full C# suite 4021/0; 5 of 10 LazyDeserialize goals
build + pass). **4 goals are blocked on the PLang builder** (planner/compiler), not
runtime. One more (`SignAndVerify`) is mine (C# `goal.call` Signature) — listed last
so you can ignore it.

All paths under `Tests/LazyDeserialize/`. **Re-test recipe** (the `cache:false`
flag does NOT bust the cache — clear it):
```bash
python3 -c "import sqlite3;c=sqlite3.connect('Tests/.db/system.sqlite');c.execute('DELETE FROM LlmCache');c.commit()"
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang '--build={"files":["LazyDeserialize/<NAME>.test.goal"]}'
../PlangConsole/bin/Debug/net10.0/plang --test    # then check the goal is [Pass]
```

## A) Three won't build — planner step-count failure (non-deterministic)
`TamperedSignedData_FailsVerify`, `NavigationOnTypeUnknown_AsksForAsType`,
`DoublePlusDecimal_Errors`.

Symptom (retries don't recover):
```
Planner validation failed: Planner returned %plan.steps.Count% step plans but goal has N steps.
🔴 BuilderPlannerFailed(400)
```
- The single-step `%!error%` probe builds green now, so the **compiler-stage** vector
  is fixed (runtime). This is the **planner-stage** issue you flagged: the planner
  echoes a step's `%!error%` / quoted-JSON into the `description` field; rendering it
  breaks the plan JSON → `plan.steps` null → the `%plan.steps.Count%` message shows
  unrendered. Your suggested mitigation (drop `description` from the planner schema,
  or stop rendering `%...%` over the plan result) is the fix — it's builder-side.
- Common trait: all three are multi-step goals with a `CaughtIt` sub-step
  (`assert %!error% is not null`) and/or literal quoted JSON in the step text.

## B) Builds, but compiler parses a quoted-string literal as JSON
`ReadConfigJson_UntouchedIsJsonString` — builds, fails:
```
assert %cfg% equals "{\"port\":8080}"
→ compiled assert: { Expected: {port:8080}(dict), Actual: %cfg% }   ← Expected got JSON-parsed
```
The RHS is a **double-quoted string literal**; the compiler turned it into a dict, so
the assert compares dict-vs-raw-string and fails. A quoted string literal in goal
text should compile to a `string` value, not be JSON-parsed. (Once fixed it passes —
`%cfg%` scalar is the raw json string, which the runtime now keeps lazy.)

## C) (mine, not builder) `SignAndVerifyRoundTrip`
Builds fine (`sign "hello world"` → `signing.sign{Data:'hello world'}`), but fails
"Data has no signature": the `Signature` is dropped when `%signed%` crosses the
`goal.call` boundary (param resolves to the value, not the Data). Runtime sign/verify
is correct (C# `Cut3`). I'll take this in a separate C# pass — no action for you.

## Acceptance
After A+B: `plang --test` shows all of `TamperedSignedData`,
`NavigationOnTypeUnknown`, `DoublePlusDecimal`, `ReadConfigJson_Untouched` as
`[Pass]`. (`SignAndVerify` stays until I land the goal-call fix.)
