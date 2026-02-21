# Learnings — runtime2-settings / Code Analyzer v1

**Source:** Tester v1 findings (`.bot/runtime2-settings/tester/v1/summary.md`)

## 1. Trace data origin to assess cast severity

I flagged `(T)value` as medium severity. The tester elevated it to CRITICAL by tracing *where the value comes from*: `System.Text.Json` boxes `20971520` as `int` (fits 32 bits), but `archive.Settings.Max` declares `long`. C# unboxing requires exact type match — `(long)(object)intBoxedValue` throws `InvalidCastException`.

**Rule:** When reviewing casts, don't just look at the cast site. Trace the data origin. Severity depends on what feeds the value, not how the cast looks in isolation.

## 2. Add a "test gap" pass to code analysis

I reviewed `Goal/Methods.cs` save/restore of `SettingsScope` and noted it was "correct." The tester pointed out it has zero test coverage — if those lines were deleted, no test would fail. Same for the 3+ level parent chain walk.

**Rule:** For every code path I review, ask: "if I deleted this code, would a test fail?" If the answer is no, that's a finding — even if the code is correct. Code correctness and proven correctness are different things.

## 3. Use deletion tests to prove coverage gaps

The tester's method for demonstrating gaps is rigorous: "if you deleted lines X-Y, no test would fail." This is stronger than saying "this isn't tested" — it's a falsifiable claim. They also provided concrete test code that *should* exist.

**Rule:** When flagging a test gap, state it as a deletion test: which lines could be removed without breaking any test. And provide the concrete test that would close the gap.

## 4. Code analysis and test analysis are complementary

My analysis was code-focused (is the code correct?). The tester's was test-gap-focused (is correctness proven?). Both perspectives are needed. I should incorporate test gap analysis into my workflow, at least for critical code paths.

## 5. Numeric widening is a recurring C# hazard with JSON

The `int` vs `long` boxing issue with `System.Text.Json` is not specific to Settings — it applies anywhere PLang deserializes JSON into typed storage and later retrieves via generic cast. Watch for this pattern in other `Resolve<T>` or generic retrieval methods across the codebase.

---

**Source:** Reviewing coder v2 fixes with fresh eyes (2026-02-21)

## 6. A fix can introduce the same class of bug it solves

The coder fixed `(T)value` with `Convert.ChangeType` — handles numeric widening. But `Convert.ChangeType` doesn't handle enums, and the very same Settings class has an enum property (`CompressionLevel Level`). The fix solved the int→long case but missed the int→enum case. When reviewing a fix, ask: "does this fix cover ALL the types that flow through this code path, not just the one that was reported?"

## 7. Review fixes against the full type surface, not just the reported type

The bug was reported as int→long. The fix was tested as int→long. But `ISettings` implementations can have any property type — `long`, `int`, `string`, `bool`, enums, etc. A generic `Cast<T>` must handle the full surface. I should have caught this in v1 by looking at `archive.Settings` and asking: "what happens when Level goes through this path?"

## 8. Context operations (Clone, CreateChild) are a family — review them together

I looked at `CreateChild()` and `Clone()` separately. CreateChild works for settings because it creates a parent chain. Clone doesn't copy SettingsScope and has no parent chain back to the original. When a new property is added to PLangContext, ALL context-creation methods must be audited: constructor, CreateChild, Clone. They're a family.

## 9. Simulation tests are weaker than integration tests

A test that manually reproduces what production code does (save/null/restore) proves the *concept* works but not that the *actual code* does it. If someone refactors RunAsync, the simulation test still passes. When testing infrastructure mechanisms (save/restore, scope isolation), prefer calling the real code path even if setup is harder.

## 10. Trust framework guarantees, test the choice

For thread safety via ConcurrentDictionary: don't test that concurrent writes work (that's testing the framework). Instead, assert the TYPE is ConcurrentDictionary — so a refactor to Dictionary breaks a test. Test your architectural decisions, not the framework's correctness. This is Ingi's insight.
