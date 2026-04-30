# Tester v3 review — summary

**Verdict:** needs-fixes.

The v2 toothlessness pattern recurs in v3's fix to v2 — Finding #1 is the same shape Ingi flagged for v2. The other 6 v2 findings were honestly closed (tester confirmed via 4 empirical deletion tests on production code — depth-bound, Step OCE, diagnostic span, pipeline cache machinery — all produced the expected failure when the fix was removed).

Test totals: **2456/2456 C# green** without coverage instrumentation. Coverage on the 3 changed production files: 83.1%, 100.0%, 75.0% — every v3-added executable line HIT.

## The 5 findings

| # | Sev | Where | Issue | Status |
|---|---|---|---|---|
| 1 | **MAJOR** | `NoDeadEmissionTests.cs:140-142` | `PublicMethodDecl` regex anchored to `^\s*public\s+...`. The v1 regression was `protected ParamData()`. Pattern B is structurally incapable of catching the regression named in its own docstring. The generator currently emits `protected static Data()` (4 overloads) and `protected static Error()` — Pattern B doesn't even examine them. **Will address.** |
| 2 | minor | `NoDeadEmissionTests.cs` | Pattern B has no synthetic regression tests. Pattern A has 5 `Heuristic_*` tests. If `PublicMethodDecl` broke or `LoadAllCallableSources` returned empty, Pattern B silently passes. **Will address — fold into #1.** |
| 3 | minor | `NoDeadEmissionTests.cs:117, 128` | `LoadAllCallableSources` concatenates raw text; `\b{name}\s*\(` matches inside `//`, `///`, and string literals. `SnapshotParams` empirically shows up in 5+ comments. After widening Pattern B to `protected`, `Data` and `Error` will be the names checked — these are common enough in comments and emission-string raw literals to false-green. **Will address — comment/string stripping. Becomes critical once #1 widens.** |
| 4 | minor | `IncrementalCacheTests.cs` | 2 `PipelineCache_*` tests fail under `--coverage` instrumentation (Roslyn `CSharpGeneratorDriver` step-tracking interacts with coverage hooks). 2454/2456 with coverage. CI risk if coverage gating is added. **Will address — document at top of test file (lowest-cost option per tester).** |
| 5 | NIT | `PLang.Generators/this.cs:20, 29` | Codeanalyzer Finding 46 carry-forward — `ActionInfoTrackingName` (unfiltered) is dead. Tester offers two options: delete OR add cache-hit test for the unfiltered step. **Will address — option (b), the stronger contract per tester.** |

## What I take from this

The v2 toothlessness pattern is **subtle and easy to miss**. The Pattern B test was named after the `__paramData/ParamData()` regression and explicitly documented to catch it. I read the docstring, wrote a regex that *looked* correct, and never empirically verified that the regex actually matched the regression shape. The v1 generator emitted `protected ParamData()` — public/protected is one keyword's difference but completely changes whether the regex fires.

**Lesson:** when a test claims to catch a specific regression shape, demonstrate it. Either (a) write a synthetic test that feeds the regression shape to the helper and asserts it's flagged, or (b) keep the regression's actual source on hand and run the test against it once before declaring victory. The 5 `Heuristic_*` tests for Pattern A do exactly (a). Pattern B should mirror that.

The widen-to-protected fix is small. The defensive-depth gap (no synthetic Pattern B tests, no comment/string stripping) is the more important learning.
