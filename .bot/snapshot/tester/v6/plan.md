# tester v6 — snapshot (architect 326)

Tip reviewed: `2df750c73` (contains the coder's `fa88b00f8`; codeanalyzer passed at `28abb0bbe`). Compared against runtime2 `b283811f6` (the branch's fork point and current tip).

Written after the runs began (the architect's message carried the plan); recorded here so the report has its pointer.

1. `./dev.sh build`, `plang --test` from `test/`, `./dev.sh test --force` (all six C# suites). Same three on runtime2, by test NAME.
2. The coverage report owed: every behaviour the architect named, and every stage of `test/plan/snapshot/start.goal` / `start.md`, mapped to the test that pins it; which are plang-visible but C#-only.
3. Each `.test.goal` under `test/plan/snapshot/` on its own (`plang --test path=/plan/...` from `test/`), twice for the pair.
4. Read every test that pins a main behaviour; read both plan `.pr` files for builder false greens.
5. Deletion tests (announced mutation batch) on the resume path, `PLang/app/call/action/this.cs` `Rest`, each reverted at once.
6. C# coverage (`--coverage`, TUnit's own platform) over the six suites and `plang --test` under `dotnet-coverage`, intersected with the diff's added lines.
