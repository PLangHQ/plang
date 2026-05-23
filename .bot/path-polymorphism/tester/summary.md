# Tester — path-polymorphism

## Version

v7 (matches coder v7 — the codeanalyzer v4 docstring response on top of coder
v6 + the intervening typed-returns sweep). Second tester pass; v5 was the
first.

## What this is

`path-polymorphism` makes PLang's `path` scheme-polymorphic. v6/v7 layer on:
coder v6 fixed slash-qualified `goal.call` resolution + inverted `File.Exists`
in the builder bootstrap + added an `Actions` filter param to `builder.actions`
+ planned (but did not ship) two structural builder validators; coder v7 was
docstring-only; in between, a 25-commit typed-returns sweep flipped 69
handlers `Task<Data>` → `Task<Data<T>>` and typed nine provider interfaces.
codeanalyzer v4 gave that sweep CLEAN-modulo-docs.

This pass validates **test honesty** on top of green tests.

## What was done

Clean rebuild (stale-binary trap), re-ran both suites: **C# 2889/2889, plang
203/203 / 0 stale, build clean.** No regressions (codeanalyzer v4's single
plang fail was an external LLM 503; not reproduced). Then:

1. **Verified v5's six findings** — 5 of 6 honestly closed. F2 (`path.Equals`
   coverage) and F3 (`assert` path-truthiness) mutation-tested red: revert
   `RootComparison` → `OrdinalIgnoreCase` and 2 `PathEqualityTests` go red;
   delete the `IBooleanResolvable` branch in `assert/code/Default.cs` and 4
   `AssertTests` go red. Both reverted, `git status` clean.
2. **Audited the new surfaces from coder v6 + typed-returns sweep.**

**Verdict: FAIL (needs-fixes).** Five findings — full detail in
`v7/result.md` and `.bot/path-polymorphism/test-report.json`:

1. **F4-CARRY (major)** — the negative-branch plang test for `if X exists`
   is written as `.test.goal2`. The runner doesn't discover `.goal2`. The
   directory listing suggests coverage that doesn't execute. Rename to
   `.test.goal`.
2. **N1 (major)** — `GoalCall.GetGoalAsync` slash-qualified resolution +
   `LoadFromFile` leaf-match (coder v6 core fix) have zero unit tests.
   Self-rebuild is the only oracle.
3. **N2 (major)** — `builder.actions`'s new `Actions` filter parameter is
   unguarded. `GetActionsTests` never sets `Actions = …`.
4. **N3 (minor)** — inverted `File.Exists` at `builder/this.cs:113` has no
   guard. `AppTests` covers Load/Save but never `Builder.@this.RunAsync()`.
5. **N4 (minor)** — `Action.ReturnTypeName` (the new property the
   typed-returns sweep was justified by) has zero test coverage.

Process note: coder v6 and v7 both shipped without `baseline-tests.md`. v5
had the same. Recoverable but worth fixing.

## For v7 after review

v5 → v7 progression on review-driven coverage:

```csharp
// v5 — PathEqualityTests.cs did not exist. Reverting N2's RootComparison fix
// kept all 2882 tests green.

// v7 — PathEqualityTests.cs:54 (FilePath_CaseVariant_HonoursRootComparison)
if (System.OperatingSystem.IsWindows())
{
    await Assert.That(lower.Equals(upper)).IsTrue();
}
else
{
    // Linux: distinct files — must compare unequal.
    await Assert.That(lower.Equals(upper)).IsFalse();
    await Assert.That(lower.GetHashCode()).IsNotEqualTo(upper.GetHashCode());
}
// Mutation-test on Linux: hard-coding OrdinalIgnoreCase fails both assertions.
```

## What to do next

Coder addresses the five findings (1 file rename, 4 small C# test files / new
tests in existing files — all mechanical). Then re-test as v8.
