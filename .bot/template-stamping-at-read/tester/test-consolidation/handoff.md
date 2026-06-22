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

## OPEN: serializer tests may be partly deletable (in progress)
The `plang` wire serializer (goal-run/.pr I/O path) **extends**
`app.channel.serializer.Json`; `Text` falls back to `Json`. So the broad I/O-runtime suite
likely already covers the `Json` base → some of the 48 collapsed serializer tests may be
**deletable**, not just collapsed.
- **Proof being run:** move the 2 serializer test files aside → run the full Wire suite with
  coverage → diff `Json.cs`/`Text.cs` against `/tmp/wire_X.json` (serializer-tests-only
  baseline). Lines the rest-of-Wire still covers = deletable; lines unique to the serializer
  tests = keep.
- **Caveat:** full-suite-with-coverage is SLOW here (447 tests, kept timing out foreground;
  run it as a background job to completion). For a *complete* redundancy answer you'd want
  cross-project coverage (Modules/Data/Runtime goal-run tests also hit serializers) — best
  as a CI/overnight job. Wire-rest alone is a conservative lower bound: delete only what it
  proves covered.

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
