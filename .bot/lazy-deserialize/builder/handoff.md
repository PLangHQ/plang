# Builder handoff — lazy-deserialize goal tests

From: coder. The lazy-deserialize **runtime** (C#) is complete and green — full C#
suite 4021/0, and **5 of 10** `Tests/LazyDeserialize/*.test.goal` build + pass,
proving the runtime end-to-end. The remaining **5 are blocked in the PLang builder
(planner), not the runtime.** Each is below with the diagnosis. Fixtures
(`config.json`, `report.csv`) are committed.

## Passing (built, green) — for reference of what the planner handles fine
- `BigIntegerSumOverflowsUInt_LandsCorrect` — set/set/`+`/assert
- `ReadConfigJson_NavigateMaterialisesField` — `read file 'config.json'` + navigate
- `AsJson_ResolvesTypeUnknown` — `set %x% = "…" as object/json` + navigate
- `HttpStatusRead_DoesNotMaterialiseBody` — `get http …` + `%response!status%`
- `ReadCsv_LandsAsTable` — `read file 'report.csv'` + type/scalar asserts

## Blocked (planner) — need builder attention

### 1–4. Planner step-count failure (`BuilderPlannerFailed`)
`SignAndVerifyRoundTrip`, `TamperedSignedData_FailsVerify`,
`NavigationOnTypeUnknown_AsksForAsType`, `ReadConfigJson_UntouchedIsJsonString`.

Symptom (retried 3–6×, non-deterministic):
```
Planner validation failed: Planner returned %plan.steps.Count% step plans but goal has N steps. — retrying...
🔴 BuilderPlannerFailed(400)
```
Two things to note:
- **The error string itself shows `%plan.steps.Count%` UNRENDERED** — the planner's
  own validation message has an unsubstituted variable. Likely the planner returned
  a malformed/empty plan (no `steps` array) and the count never bound. Worth fixing
  the message *and* the underlying "planner returned no steps" path.
- Common trait of the failing goals: **literal quoted JSON** in the step text
  (`set %x% = "{\"port\":8080}"`, `assert %cfg% equals "{\"port\":8080}"`) and/or
  multi-step goals with `on error call`. The builder's own error hints at this
  ("long quoted strings … can confuse the planner").

Sign goals were reworded to idiomatic form already (`sign "hello world", write to
%signed%` / `verify %signed%`) — they still hit the planner failure, so this is
planner robustness, not goal text.

### 5. `DoublePlusDecimal_Errors` — number-literal kind
Builds fine, but **fails because `set %a% = 1.5` stores 1.5 as `decimal`, not
`double`** (the .pr loader reads a bare JSON number via `TryGetDecimal` before
`GetDouble`). So `%a% + %b%` is decimal+decimal (no error), and the test — which
expects double⊕decimal to raise — sees no error.
Fix options (builder/number-literal call): a bare float literal `1.5` should infer
`double`, OR the goal needs `set %a% = 1.5 as number/double`. The C# arithmetic
(double⊕decimal → `PrecisionMixRequiresChoice`) is correct and proven in
`NumberArithmeticTests` / `Cut5`.

## Runtime side is done
All the access-driven-resolution plumbing that makes a lazily-read value survive
`read → write to %var% → navigate/assert` lazily is landed (see the
`stage 5 (goal-level lazy)` commit): `ShouldExit`, `AsCanonical`, `variable.set`
raw passthrough, `Variables.Set` events, navigation `.Type`, `assert` scalar
compare, `object/json` JsonElement unwrap, http `!status`. No runtime changes are
needed for the 5 blocked goals — only the planner builds them.
