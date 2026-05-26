# v2 review summary — what I flagged, what changed

## v2 verdict: FAIL — 21 PLang tests red

v2 (re-issued) failed on the strict rule: any red test = FAIL regardless of blame or baseline. The 21 failures were a mix of missing `.build/*.pr` artifacts and real behavioral failures (NullRef in condition.if, wrong Audit depth, etc.).

## v3 coder changes

| Commit | What changed |
|--------|--------------|
| 7ed35b550 | runtime/builder: path canonical form, JSON serialization, .template MIME |
| 7fa6d16ad | tests: rewrite truncated path/prPath in committed .pr files |
| ff9dee864 | condition/builder: clean up runtime guard + teach builder LLM correctly |
| 606689e62 | tests: rebuild .pr files for fixes from the updated builder teaching |

The rebuild closed the "File not found: .build/*.pr" failures, the path canonical-form fix closed the truncated-path issues, and the condition.if builder teaching closed the NullRef path.

## v3 result

- **C# suite:** 3036/3036 pass (unchanged from v2 — wasn't broken).
- **PLang suite:** 212 pass / 6 fail / 218 total (v2 was 196 / 21).
  - The 6 remaining failures are all `*.fixture.goal` files designed to fail (they back the test.report tests). Discovery picks them up despite their intentional-failure status. Two unique fixtures × two paths (TestModule/Report and Modules/Test/Report) × duplication.
- **Builder smoke test added:** `Tests/BuilderSanity/BuilderSanity.test.goal` + 3 helpers exercise set/foreach/if/call. Run with cache=false to validate the builder before trusting `plang --test`.

## Process

- No `coder/` folder still exists on this branch. Three coder versions worth of work without a single `coder/v<N>/plan.md`, `summary.md`, or `baseline-tests.md`. Flagging again.
- Mutation-by-reasoning still holds for the v1 → v2 test additions (output capture, Timings, cost math).
