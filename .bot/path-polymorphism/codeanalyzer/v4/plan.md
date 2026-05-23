# codeanalyzer v4 — plan

Reviewing all post-v3 work (commits `12f192c2e..2aab57daf`):

1. **Coder v6** — slash-qualified `goal.call` resolution, builder validators, inverted `File.Exists`, `builder.actions` `Actions` filter param.
2. **Strongly-typed returns sweep** — ~25 commits flipping 69 action handlers
   from `Task<Data>` → `Task<Data<T>>`, plus provider-interface typing
   (`IPath`, `IIdentity`, `IStore`, `ISigning`, `ICrypto`, `ITemplate`, `IEvaluator`, `IAssert`, `ILlm`).

## Five-pass focus

- **Pass 1 (OBP)** — does the sweep introduce any cross-file mutable
  collections, lock-target leaks, or duplicated state? Probably no (mechanical
  signature changes) — confirm.
- **Pass 2 (simplification)** — bare `Data` returning a typed value silently
  drops the type. Are any `T` picks wrong? Are there double-wraps (the
  documented footgun)? Any dead code from the catalog `null`-bailout path that
  should have been removed?
- **Pass 3 (readability)** — naming consistency: `Data<path>` vs `Data<object>`
  for actions that *could* be typed. Catch any `Data<object>` that should be
  concrete.
- **Pass 4 (behavioral)** — `GoalCall` slash resolution: does it cover all
  call sites? `LoadFromFile` leaf-match logic safe? Builder validators: do
  they over-flag? `Data.FromError<T>` correct? Action.this.cs new lines.
- **Pass 5 (deletion test)** — every new line earns its place? The
  hand-patched `BuildStep/start.pr` Compile step 1 — temporary or permanent?

Output: `result.md` with line-cited findings, `verdict.json`, `summary.md` update.
