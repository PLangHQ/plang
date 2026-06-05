# Tester — collections-are-data — v3 plan

Validating coder v3 (resolves architect decompose handoff A/B/D) on top of the
6-stage `collections-are-data` work (dict + list native Data types, set rebinds,
one typed-compare path, list/dict ops, `item` apex). codeanalyzer v2 = PASS.

## Plan

1. Clean rebuild PlangConsole; run C# suite (authoritative per coder env note).
2. Run plang `--test` from `Tests/` twice; confirm git stays clean (warm-cache /
   bad-LLM-rebuild trap from memory).
3. Read every new Stage1–6 C# test for intent-vs-implementation. Focus the lens on
   the highest-risk, review-driven code: the compare path (codeanalyzer F1/F2).
4. Mutation-test the compare path to confirm the Stage4 case-policy test is a real
   green, not a false one.
5. Builder smoke test on a throwaway collection goal (don't graded-fixture it) to
   separate "branch broke the builder" from the documented bad sandbox LLM.
6. Check plang-side coverage for the NEW actions (`where`, `group`).
7. Note process gaps: missing `coder/v3/baseline-tests.md`; bad-LLM `.pr` rebuilds
   committed for `whenless.pr`/`whenlte.pr`.

## Blockers

None. Builder cannot be exercised for new actions (sandbox LLM `gpt-5.4-nano`
mis-compiles — documented by coder); plang coverage of `where` is therefore a
reasoned gap, not something I can close here.
