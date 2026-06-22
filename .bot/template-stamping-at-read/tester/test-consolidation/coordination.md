# Test-consolidation — tester/coder coordination

Both tester (`plang-tester-_interactive`) and coder (`plang-coder-_interactive`) are
migrating C# unit tests to the goal-run pattern (`Make.Goal → RealGoalLoad.ViaChannel →
RunGoalAsync`) on this branch. To surface collisions early, we claim modules here before
touching them. **Read this before editing a test module; append your claim.**

Guide: `Documentation/v0.2/writing-tests.md`. Method: convert observable behavior to
goal-run, keep the named floor (build-time / no-language-surface) as C# units, verify
parity with a cobertura line-set diff before deleting, then commit per module.

## Lanes

| module | owner | status | notes |
|--------|-------|--------|-------|
| variable | tester | **done** | 34→32, coverage parity proven. `VariableGoalRunTests` + `SetBindingContractTests` (floor). |
| condition | coder | in progress | coder migrated IfHandler/orchestration (commits `a805dbc3`, `2fd09f68`); tester stays out. |
| loop | coder | mostly migrated | tester stays out. |
| file | coder | partial | tester stays out. |
| list | tester | **done** | 39→41, coverage parity. `ListGoalRunTests` + floor `ListAddIdentityTests` (ref-identity) + `ListFlattenRecursionTests` (recursion needs raw nested seed). |
| TypeMappingTests (enumeration) | tester | **done** | **90 → 21** (77% cut), coverage parity. 73 table-row tests → 4 data-driven loops; 17 distinct conversion paths kept. This is where real removal lives. |
| DataTests (enumeration) | tester | **done** | **96 → 79** (−17), parity. Tables collapsed (IsVariable/HasVariableReference/IsEmpty/Path/ToString 23→5); ~73 distinct Data behaviors kept. Mixed file, so smaller cut than TypeMapping. |
| EngineTypesTests (enumeration) | tester | **done** | **88 → 31** (−57, 65%), parity. 5 tables collapsed (Clr/Name/Kind/Mime/Compressible 62→5); registry Add/Remove/KindOf + BuilderNames + ComplexSchemas + depth-limit kept. |
| VariablesTests | tester | **done** | **69 → 63** (−6), parity. Mixed file (3 classes); only Contains/Remove/GetValue/Get_Generic were tables. Most are distinct Set/Get navigation branches that stay. |
| AssertTests (enumeration) | tester | **done** | **36 → 14** (−22, 61%), parity. Value-based asserts (Equals/NotEquals/IsTrue/IsFalse/IsNull/IsNotNull/Contains/GreaterThan/LessThan) → 9 data-driven; 4 file-path cases + custom-message kept. |
| Json/TextStreamSerializerTests | tester | **done** | **68 → 48** (−20), parity. Uniform Serialize/Deserialize scalar+value rows collapsed; distinct cases (datetime/guid/bytes/culture-decimal/object/async/stream/error) kept. |
| math | tester | **done** | **17 hand-built `.Run()` → MathGoalRunTests (12 methods, 17 cases)**, coverage parity (0 math-prod lines lost). Altitude conversion: arithmetic now dispatches through `Make.Goal → RealGoalLoad.ViaChannel → RunGoalAsync`; operands born-typed from values; assert on returned `Data<number>` via `result.Value<number>()`. |

### RESOLVED — serializer unit tests are NOT deletable (kept collapsed)
Ran the redundancy proof (2026-06-22): serializer-tests-only coverage of `Json.cs`/`Text.cs`
vs. rest-of-Wire coverage (the two test files moved aside, Wire rebuilt, full 447-test suite
run with cobertura — completed in ~4s, the earlier "timeout" did not recur).
Result: the collapsed serializer tests **uniquely cover 22 lines of `Json.cs` and 32 of
`Text.cs`** that no other Wire test touches. Those unique lines are all **structurally
unreachable from happy-path goal-run**: the serialize/deserialize `catch` blocks
(`JsonException`/`IOException`/`NotSupportedException`), the generic `DeserializeAsync<T>` /
`Deserialize<T>` typed paths, empty-stream guards, `WithIndentation()`, and the entire
`Text.cs` deserialize side (`DeserializeAsync(stream)`, `FromText<T>`). You can't make the
wire path throw IOException or feed it malformed bytes through a normal goal run, so
cross-project goal-run tests (Modules/Data/Runtime) won't reach them either. Verdict: keep
collapsed-not-deleted (already the committed state). Open question closed.

### Heuristic caveat (learned)
Prefix-clustering over-counts: `RenderTests` is 33 `Render_*` but each is a distinct
scenario (inline/file/callgoal/include) — NOT collapsible. A real table is
input→output rows with identical shape (AssertEquals{Expected,Actual}.Run() → pass/fail).
Check a few bodies before assuming a high-cluster file is tabular.

### Diminishing returns — where the big cuts actually are
Pure-enumeration files give 60–77% cuts (TypeMapping 90→21, EngineTypes 88→31). Mixed
files give ~10–18% (DataTests 96→79, VariablesTests 69→63) because most of their tests are
distinct behaviors, not table rows. The remaining big wins are other *pure-table* files —
worth grepping for `GetType_/Kind_/Name_/Mime_`-style one-row-per-method clusters rather
than grinding mixed files for single-digit cuts.

### Gotcha for whoever edits DataTests.cs (and similar)
DataTests.cs holds **two classes** (`DataTests` + `DynamicDataTests`). A script that
inserts "before the final `}`" lands in the wrong class and the new tests run under the
wrong node — caught only because the coverage gate showed phantom lost lines. Always
confirm new tests register under the intended class (`--list-tests` + a class-scoped
coverage run), not just that the build is green.

**Removal lives in enumeration files, not module-action tests.** variable/list were ~1:1
(distinct branches → little to cut). The big cuts are the table files: TypeMappingTests
(done, 90→21), and next DataTests (96), EngineTypesTests (88), VariablesTests (69) — same
shape (one mechanism typed N times → one data-driven test).

### Note for coder — wire flattens nested list literals
Seeding a nested list via `variable.set` through `RealGoalLoad.ViaChannel` (the .pr wire
read) arrives **flat** — `[1,[2,3]]` materializes as `[1,2,3]`. So flatten's recursive arm
is unreachable from a language-seeded list; only a raw `List<object?>` seed (direct
`Variable.Set`, no wire) preserves nesting. Flagging in case nested-list-through-wire is a
real round-trip bug rather than expected normalization.

## Conflict protocol
- One module = one owner at a time. Don't edit a module another bot owns.
- Shared infra (`PLang.Tests/Shared/Make.cs`, `RealGoalLoad.cs`, `TestApp.cs`): if you
  need a new helper, add — don't reshape existing signatures without a note here.
- If a cherry-pick / parallel edit conflicts, the second committer resolves and notes it
  in this file under "Observed conflicts" so we learn where the seams are.

## Observed conflicts
(none yet — variable cherry-picked onto the branch tip cleanly)
