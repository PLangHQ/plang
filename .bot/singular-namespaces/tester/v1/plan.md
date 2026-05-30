# Tester v1 — singular-namespaces

## Context

Big refactor branch: plural→singular `PLang/app/**` namespaces (Stage 1), non-null
`app`/`context` invariants (Stage 2), accessor reshape (Stage 3), type-entity move +
Entry fold (Stage 4). Test-designer wrote 52 C# contract tests + 5 PLang `.test.goal`
files as `Assert.Fail`/`throw "not implemented"` stubs — the names/comments are the spec.
The coder filled them in across partial stages.

**Conflicting claims to resolve by running:**
- coder `report.md`: 3683 pass / 11 fail, Stage 4 deferred, "49/52 contract tests."
- coder commit `fd6e4e367`: "3694 passing, 0 failing. The branch is complete."
- codeanalyzer v3 verdict: "3694/3694 suite green."
- `report.md` is **stale** — Stage 4 Entry-dissolve actually landed in `a94d03a54`
  (`Field` lifted to `app.type.Field`, `Types` holds entities, Entry/EntryKind gone).

## What I will check

1. Clean rebuild + full C# suite (ground truth pass/fail).
2. PLang suite (`cd Tests && plang --test`).
3. The **inverted Stage 2 NullabilityTests** — coder rewrote all 7 from the
   architect's "throws hard / fallback removed / back-refs non-null" contract to the
   opposite "stays nullable / fallback stays," citing "Per Ingi." Verify the reversal
   is recorded; assess whether the rewritten tests honestly verify the *chosen* contract.
4. The Stage 4 **builder golden** test — does it actually byte-compare, or assert non-null?
5. The 5 PLang `.test.goal` bodies vs their stated intent; read each `.pr` for
   builder false greens (step text ↔ actions[0].module.action).
6. Coverage on changed C# (best-effort).

## Hypotheses going in (highest false-green risk first)

- Stage 2 tests inverted to make deferred work appear complete (review-driven, highest risk).
- Golden test gutted to non-null assertions.
- PLang `DataTypeReadsEntity` body never reads `.Type`.
- `ChannelIndexMissThrows` asserts only success-of-error, not Error.Key/StatusCode.

## Process gaps noted

- No coder `baseline-tests.md` (can't cleanly separate regression vs pre-existing).
- coder `report.md` stale vs HEAD.
