# Test consolidation — handoff / status

**Branch:** `template-stamping-at-read` (shared with coder `plang-coder-_interactive`).
**Owner:** tester. **Goal (Ingi):** cut the ~4,099 C# test methods hard, without losing
coverage. Method: collapse table-enumeration tests to data-driven; convert hand-built
`.Run()` unit tests to goal-run where it adds altitude; **prove zero coverage loss with a
cobertura line-set diff before every commit.**

Read first: `Documentation/v0.2/writing-tests.md` (the rule + the gate), `coordination.md`
(lane table + per-section results + lessons).

## Done (8 sections, all pushed, each coverage-gated to ZERO lines lost)
| section | before→after | kind |
|--|--|--|
| variable | 34→32 | goal-run conversion + floor split |
| list | 39→41 | goal-run conversion + floor split |
| TypeMappingTests | 90→21 | pure enumeration collapse (77%) |
| DataTests | 96→79 | mixed: tables collapsed, behaviors kept |
| EngineTypesTests | 88→31 | pure enumeration collapse (65%) |
| VariablesTests | 69→63 | mixed: small tables only |
| AssertTests | 36→14 | enumeration collapse (61%) |
| Json/TextStreamSerializerTests | 68→48 | uniform serialize/deserialize rows |

Cumulative ≈ **−191 methods**. Every push was a clean fast-forward (no coder conflict).

## The pattern (so the next person picks targets well)
- **Pure-table files** (one trivial input→output row per method, identical shape) give
  **60–77%** cuts → collapse to one data-driven test looping the table (`.Because(label)`
  so a failing row names itself).
- **Mixed files** (shared prefix but distinct behaviors/branches) give **<20%** — only the
  genuine sub-tables collapse; don't force the rest.
- **Prefix-clustering over-counts**: `RenderTests` is 33 `Render_*` but each is a distinct
  scenario — NOT a table. Always read a few bodies before assuming tabular.
- **Two-class trap**: several test files hold 2+ classes (DataTests, VariablesTests,
  the serializer files have a trailing helper class). A script that inserts "before the
  final `}`" lands in the wrong class — the new tests then run under the wrong node and
  coverage shows phantom losses. Insert before the next `class` declaration; verify with
  `--list-tests` + a class-scoped coverage run.

## The gate (mechanics)
```
dotnet build PLang.Tests/<Proj>/PLang.Tests.<Proj>.csproj
DLL=PLang.Tests/<Proj>/bin/Debug/net10.0/PLang.Tests.<Proj>.dll
# baseline BEFORE editing (or via git stash of the test file):
dotnet exec $DLL --treenode-filter "/*/*/<Class>/*" --coverage --coverage-output-format cobertura --coverage-output base.cobertura.xml
# after: same, then diff covered (file,line) sets restricted to the production files under test.
```
Filter quirks: namespace-folder filters can match multiple classes; `!`-negation does NOT
work. Method-level filters were flaky — class-level (`/*/*/<Class>/*`) is reliable.

## RESOLVED: serializer tests are NOT deletable — keep collapsed (2026-06-22)
Ran the proof. Moved `Json/TextStreamSerializerTests.cs` aside, rebuilt Wire, ran the full
447-test suite with cobertura coverage (completed in ~4s — the earlier foreground "timeout"
did not recur), and diffed `Json.cs`/`Text.cs` covered-line sets against the
serializer-tests-only baseline.
- **Json.cs:** ser-tests cover 71 lines, rest-of-Wire covers 49 of them → **22 unique** lines.
- **Text.cs:** ser-tests cover 53 lines, rest-of-Wire covers only 21 → **32 unique** lines.
- The unique lines are all **error catch-blocks** (`JsonException`/`IOException`/
  `NotSupportedException`), **generic typed-deserialize** (`DeserializeAsync<T>`/`Deserialize<T>`),
  **empty-stream guards**, `WithIndentation()`, and the whole `Text.cs` deserialize side
  (`DeserializeAsync(stream)`, `FromText<T>`). None are reachable from happy-path goal-run —
  you can't force an IOException or malformed bytes through the wire path — so cross-project
  goal-run coverage won't reach them either. The within-Wire lower bound is therefore also the
  complete answer: **not deletable.** Collapsed-not-deleted is the right final state.

## Remaining candidates (vet bodies first — prefix ≠ table)
- `DefaultEvaluatorTests` (49, all `Evaluate_*`) — biggest remaining table, BUT it's in
  `condition`, the **coder's lane**. Coordinate before touching.
- Mostly behavior-rich (low yield): GoalsTests (40), IdentityHandlerTests (37),
  RequestActionTests (36, needs HttpTestServer), RenderTests (33, NOT tabular).
- Honest read: the big enumeration wins are largely captured. Further cuts are either
  small (mixed files) or require the goal-run *altitude* conversion (variable/list style),
  which raises coverage quality but not method count much.

## Discipline
- Pull (`git fetch` + rebase) before every commit — coder shares the branch.
- Commit only test files + `.bot/`; never production source.
- Clean `bin/.../TestResults/*.cobertura.xml` before committing.
