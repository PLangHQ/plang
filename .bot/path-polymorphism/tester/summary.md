# Tester — path-polymorphism

## Version

v5 (matches coder v5 — the codeanalyzer v2 N1–N3 response). First tester pass
on this branch.

## What this is

`path-polymorphism` makes PLang's `path` scheme-polymorphic: an abstract `path`
base, `FilePath` / `HttpPath` subclasses, a per-App scheme registry, file
handlers collapsed onto `path.X()`, and the old `System.IO.Abstractions` wrapper
layer removed. v5 is the coder's response to codeanalyzer v2's three findings:
N1 (restore `file.exists` auth gate), N2 (`path.Equals`/`GetHashCode` →
`RootComparison`), N3 (dedup `assert.ResolveTruthy`).

This pass validates **test honesty** — would the tests fail if the code were
subtly wrong? — not code correctness (codeanalyzer v3 already passed that).

## What was done

Clean rebuild (stale-binary trap), re-ran both suites: C# **2882/2882**, plang
**203/203 / 0 stale** — reproduces the coder/codeanalyzer numbers exactly, no
regressions. Then read the path-polymorphism C# suite and deletion-tested the
three v5 review-driven fixes.

**Verdict: FAIL (needs-fixes).** Six findings — full detail in `v5/result.md`
and `.bot/path-polymorphism/test-report.json`:

1. **(major, false-green)** `HandlerShapeTests.FileModule_PlangBehaviour_UnchangedFromProgramPerspective`
   is `Assert.That(true).IsTrue()` — a named test that verifies a literal.
2. **(major, missing-coverage)** N2 — `path.Equals`/`GetHashCode` has zero
   tests; reverting it keeps the suite green.
3. **(major, missing-coverage)** N3 — `assert`'s path-truthiness branch has zero
   tests; deleting it makes `assert %missing_path% is true` wrongly pass.
4. **(major, missing-plang-test)** F3's negative branch (`if missing exists` →
   false) has no plang `.goal` — `ConditionFileExistsSubSteps` tests the true
   branch only and passes even if F3 is reverted.
5. **(minor, weak-assertion)** `File.test.goal` `/ exists` uses `assert %info%
   is not null` — passes regardless of file existence.
6. **(minor, weak-assertion)** `PLangFileSystem_Absent...` test has a stale
   comment ("honest red") and probes one hard-coded namespace.

The C# contract suite is genuinely strong where it counts — `PathSchemeContractTests`,
the F3 two-branch oracle, the N1 denied-probe oracle. The failure is about the
review-driven edges (N2/N3) shipping untested and one placebo test, not about
the feature being broken.

## Code example — the false green at the centre of the verdict

```csharp
// HandlerShapeTests.cs:153 — finding 1. A [Test] named as a behaviour guard.
[Test] public async Task FileModule_PlangBehaviour_UnchangedFromProgramPerspective()
{
    await Assert.That(true).IsTrue();   // verifies nothing
}

// finding 3 — delete the resolvable branch in assert's ResolveTruthy and a path
// falls through to IsTruthy(object) -> `return true` for any non-null object.
// `assert %missing_path% is true` then passes. No AssertTests test passes a path.
```

## What to do next

Coder addresses the six findings (≈3 C# tests, 1 plang `.goal`, 1 test
deleted/rewritten, 1 comment fixed — all mechanical). Then re-test as v6.
