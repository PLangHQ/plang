# tester v4 — plan

Coder v4 closed the v3 verdict (see `v3_review_summary.md`). My v3 red
was self-inflicted: a pre-test `plang build /BuilderSanity --cache=false`
regenerated the `.pr` with a string literal, which then failed the suite.
Coder v4 rewrote the fixture to use the unambiguous list-literal form so
the builder has no degrees of freedom, and wrote the missing
`baseline-tests.md`.

## Validation strategy

1. **Clean rebuild** per stale-binary protocol.
2. **C# suite** — `dotnet run --project PLang.Tests`.
3. **PLang suite — DO NOT pre-rebuild any test fixtures.** Run
   `plang --test` straight off the committed `.pr` files.
4. **Robustness check on the BuilderSanity fixture** — what v4 actually
   asks for. Rebuild it with `cache=false` 2-3 times in succession,
   confirm:
   - the `.pr` is byte-stable across runs (or at least functionally
     equivalent — `%items%` consistently a list, not a string), and
   - the test still passes after each rebuild.
   Revert any `.pr` changes before final commit.
5. **V1 fixture re-mutation** — re-verify
   `StoreView_PropagatesIntoInnerDataProperties_NotHardcodedToOut`
   still catches the `_view` → `View.Out` mutation. Coder v4 didn't
   touch json.Writer but a full rebuild + new generator pass could in
   principle drift something. Re-run, revert.

## Outputs

- `v3_review_summary.md` (done)
- `plan.md` (this)
- `verdict.json`
- `summary.md` overwrite at bot root
- `test-report.json` at branch root
