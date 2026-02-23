# Learnings — Code Analyzer (consolidated across 3 branches)

**Branches reviewed:** runtime2-settings, data-envelope-architecture, runtime2-terminology-fix

---

## Process Failures — How I Should Work Differently

### 1. Go deep unprompted

On runtime2-settings, I did surface-level verification and stopped. Ingi had to push me: "if I missed those 3 things, what other could I have missed." I should always do the higher-level pass — trace data origins, check the full type surface, look for silent behavioral changes — without being asked.

### 2. Predict risk areas, don't just find them

I found stale `"HandlerError"` in test data through grep. But I should have *predicted* that test data is a distinct rename surface — test values don't cause failures when stale because they're arbitrary inputs to formatting tests. Prediction shows understanding; finding shows diligence. Before grepping, list the risk areas first, then verify.

### 3. Verify completion claims independently

The coder said "zero remaining references." I trusted it initially. Always run my own search. Self-reported completion misses edge cases, especially in test data and string literals.

### 4. Do behavioral reasoning after mechanical verification

On the terminology fix, I verified all strings were renamed (mechanical) but didn't ask: "if external code checks `error.Key == 'HandlerError'`, this rename silently changes behavior." After confirming the rename is done, always ask: "what could this change *break silently* — where build passes and tests pass but runtime behavior changed?"

---

## Analytical Framework — What to Check

### 5. Trace data origins to assess severity

Don't assess a cast/conversion in isolation. Trace where the value comes from. `(T)value` looks fine until you realize `System.Text.Json` boxes `20971520` as `int` but the target declares `long`. Severity depends on what feeds the value.

### 6. Review against the full type surface, not just the reported type

A bug reported as int→long gets fixed and tested for int→long. But if the generic path handles `long, int, string, bool, enum`, the fix must cover all of them. Always check: what are ALL the types that flow through this code path?

### 7. Clone/copy methods are a family — audit together

When a property is added to any object, check ALL methods that create copies: constructor, Clone, CreateChild, factory methods, deserialization. `PLangContext.Clone()` missed `SettingsScope`. `MemoryStack.Clone()` missed `Context`. Same bug, different branch. Always review the family.

### 8. Ask the deletion test: "if I deleted this code, would a test fail?"

For every code path reviewed, ask this. If the answer is no, that's a finding — even if the code is correct. Code correctness and proven correctness are different things. State gaps as: "lines X-Y could be removed without breaking any test."

### 9. A fix introduces new surface to analyze

Every fix round adds code that itself needs review. Security hardening (depth limits, cycle detection) adds code paths. Catch clause narrowing can miss exception types. Reviews should focus disproportionately on new code from the previous fix round.

### 10. Generic exception catches mask specific error types

When a new throw site is added (e.g., `InvalidOperationException` for depth limit), trace ALL catch sites that might intercept it. `catch (Exception)` wrapping everything as "JsonParseError" silently swallows the new distinct error.

---

## Design Judgment — What NOT to Test

### 11. Test the design choice, not the framework

For ConcurrentDictionary: don't test concurrent writes (that's testing Microsoft's code). Assert the TYPE is ConcurrentDictionary — so a refactor to `Dictionary` breaks a test. Test architectural decisions, not framework correctness. (Ingi's correction.)

### 12. Simulation tests are weaker than integration tests

A test that manually reproduces what production code does (save/null/restore) proves the concept but not the actual code path. When testing infrastructure mechanisms, prefer calling the real code even if setup is harder.

### 13. Rehydration heuristics are fragile across user data

Key-name-based detection (`{"value": ...}` means it's Data-shaped) breaks when user data has the same keys. Heuristics that work for internal pipeline data may false-positive on arbitrary user input.

---

## Rename-Specific Learnings

### 14. Test data is a distinct rename surface

Production code produces a string, test assertions check for it, but test *data* (constructor args for mock objects) can use the old string without failures. The test passes because it tests formatting, not the key value. Invisible to "build + pass" verification.

### 15. String literals in non-obvious places

Source generator namespace strings, error key strings, and default parameter values are all rename surfaces that find-and-replace on identifiers won't catch. Verify string literals separately.

### 16. Named tuple field renames break call sites silently in some patterns

`(ICodeGenerated? Handler, IError? Error)` → `.Action` — call sites using `.Handler` break at compile time (good). But destructuring `var (handler, error) = ...` is unaffected by the field rename (the variable name is local). This means destructuring call sites are safe but property-access call sites need updating.

---

## Meta

### 17. Cross-branch patterns recur

The same bug class appeared on two branches: Clone missing a new property. Once you find a pattern on one branch, actively look for it on every other branch you review.

### 18. Finding quality matters more than finding quantity

Some findings were genuinely valuable (decimal precision, Clone context). Others were noise (ConcurrentDictionary concurrency, key format convention). Sharper judgment about what's actually a risk vs. theoretical completeness.
