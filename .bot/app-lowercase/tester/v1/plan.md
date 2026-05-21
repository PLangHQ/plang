# Tester v1 — app-lowercase post-merge integration check

## Context

`app-lowercase` had completed 12 commits of rename + 7 OBP-merge work (coder
v1–v3, codeanalyzer PASS on 2026-05-18). Today (2026-05-18) I merged
`origin/runtime2` into `app-lowercase` — 5 commits from
`runtime2-foundation-verify` (error.handle recovery-value tests +
todos.md reconciliation). The merge resolved cleanly with no conflicts.

This is my first tester session on the branch. I'm validating that the
merge result is healthy — meaning: the rename work and the foundation-verify
work compose without regression, and the 3 new error.handle tests merged
in from runtime2 are real green (not builder false greens or weak-assertion
greens that happen to ride past the renamed namespaces).

## What I'm checking

1. **Clean rebuild from zero** — no stale-binary risk. `dotnet build PlangConsole`
   must produce 0 errors.
2. **C# suite** — full `dotnet run --project PLang.Tests`. Compare to coder
   v1 baseline (2752/2752 pre-rename on `199b4997`).
3. **PLang suite** — `cd Tests && plang --test`. Compare against baseline
   (203 pass + 6 expected-fail fixtures). Expected post-merge: +3 from the
   new `Errors/*RecoveryValue*` goals.
4. **Builder false-green check** on the 3 new `.pr` files merged in. Read
   each step's `text` and confirm `actions[*].module.action` matches it
   semantically. Specifically: `error.throw` + `error.handle` modifier with
   nested `variable.set` Actions list — not a flat `error.handle` masquerading
   as `error.throw`.
5. **Assertion quality** on the 3 new tests. The recovery-value bug fix
   landed 2026-04-27; tests must pin the *value* AND the *handled flag*,
   otherwise an `Ok()`-returning regression slips by.
6. **No regression in pre-existing tests** that exercise the renamed namespaces
   (App→app, Type→type, Path→path). Specifically watch for any test that
   was previously green and is now red — that would mean the rename broke
   reflection-driven discovery somewhere.

## Out of scope

- Re-reviewing the rename itself (codeanalyzer did the OBP shape pass).
- Coverage analysis — this version adds no production C# changes that need
  coverage; it's a merge integration check.
- Pre-existing "Untested branches" warnings in conditional tests — those
  are noted in baseline as pre-existing gaps.

## Expected verdict

PASS if: build 0 errors, C# 2752/2752, PLang 206 pass (+3 from merge) + 6
expected fixtures, all 3 new .pr files semantically aligned, all 3 new
tests assert both side-effect AND `%!error% is null`.

FAIL if: any of the above misses, OR any pre-existing green test went red.
