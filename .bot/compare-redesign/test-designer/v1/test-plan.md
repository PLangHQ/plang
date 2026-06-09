# Test plan — typed value model (compare-redesign)

Tests written: **125 C# (TUnit)** across 14 files in `PLang.Tests/App/CompareRedesign/` + **15 PLang `.goal`** under `Tests/CompareRedesign/`. All stubbed (`Assert.Fail("Not implemented")` / `throw "not implemented"`). Build verified: `dotnet build PLang.Tests` → 0 errors.

## C# (TUnit)

| File | # | Coverage |
|---|---|---|
| `Stage1_ComparisonEnumTests.cs` | 1 | enum body & sign-free contract |
| `Stage2_ValueDoorTests.cs` | 13 | `Value()` lazy/sync-complete, `Peek()`, `_raw` dissolved, no public sync `.Value`/`ToRaw`, ToString/Equals/GetHashCode never navigate, value-always-typed, `data.Type` getter trivial |
| `Stage2_PlaneResolverTests.cs` | 8 | `.` vs `!`, `!type`/`!type.list`, reserved core protected, `@schema` blocked as key, `name` removed from envelope, no shadowing |
| `Stage2_GetParameterLazyTests.cs` | 7 | `GetParameter<T>` lazy; guard fires after await; typed error not NRE; `__ResolveData` wrapper removed; await→guard→use migration |
| `Stage2_NavigationAsyncTests.cs` | 6 | `ValueTask` chain sync-completing; awaited once; no-sync-over-async gate; `read X` no I/O; Fluid materialises up-front |
| `Stage3_ReferenceNarrowTests.cs` | 15 | `read` → file/url/generic; content-kind inference; narrow mutates same Data; chain accumulates; `!type.list`; chain-wide `!` both branches; idempotent; clones don't propagate; terminals don't narrow |
| `Stage3_PathDemolitionTests.cs` | 15 | no Content/Source; `_location` private; Write emits as-typed; `!absolute` gated; `!extension` serialised; text has no Path; `directory.list : list<path>`; url fetch vs host-without-fetch; file write-out pre/post narrow; `set %dot%` rebind |
| `Stage4_RankTests.cs` | 4 | static rank specificity; takes whole `Data`; no value read; `item` doesn't implement |
| `Stage4_PerTypeCompareTests.cs` | 17 | text/number + cross (5 tests prove the trio); replicate across date/time/datetime/duration/list/bool/binary/choice/dict; null carve-out; nulls last; Incomparable; sync core |
| `Stage5_DataCompareEntryTests.cs` | 4 | caller-order; two awaits then sync; ranking no read; no Type.Name switch |
| `Stage6_ConsumersTests.cs` | 17 | `if`/assert boundary mapping; two-phase sort; membership never errors; Pile-2 sites (sqlite/openai/identity/fluid); old mediator/ScalarComparer/NormalizeTypes/IEquatableValue deleted |
| `Stage6_DiffRenameTests.cs` | 2 | rename `Compare`→`Diff`; same diff trees |
| `Stage7_SurfaceGateTests.cs` | 10 | gate fails public CLR returns; `IsTruthy : @bool` passes; internal plumbing untouched; gated interop exempt; representative `!`-surface members typed (path.absolute, text.length, dict.keys, list.count, file.size) |
| `Stage7_PathGrowthTests.cs` | 4 | `path.IsUnder`/`path.Kind`; `.Relative`/`.Extension` internal |
| **Total** | **125** | |

## PLang `.goal`

| Folder | Behaviour |
|---|---|
| `Cut1_CrossTypeAntisymmetry/` | Integration cut 1 — cross-type antisymmetry |
| `Cut2_LazyReadAndNarrow/` | Integration cut 2 — lazy read + narrow + chain-wide `!` + param-path |
| `Cut3_WriteOutDirIsListing/` | Integration cut 3 — `write out %dir%` listing not content |
| `Cut4_SortByIoKey/` | Integration cut 4 — `sort by size` two-phase |
| `Cut5_EnumBoundaryAndMembership/` | Integration cut 5 — enum boundary + membership + null carve-out |
| `Cut6_ReadThenScalarYieldsContent/` | Integration cut 6 — `read`-then-scalar still yields content |
| `Failure_DictOrderNumber/` | Failure matrix — `if %dict% > %n%` → error |
| `Failure_DictEqNumber/` | Failure matrix — `if %dict% == %n%` → error |
| `Failure_DictOrderDict/` | Failure matrix — `if %d% < %d2%` → error |
| `Failure_SortMixedIncomparable/` | Failure matrix — sort mixed → error |
| `Failure_ParamResolutionError/` | Failure matrix — bad scheme → typed error |
| `Plane_DataKeyVsProperty/` | `%x.size%` vs `%x!size%` no shadow |
| `Plane_NullComparisonsWork/` | `%x% == null` works for every type |
| `Plane_MembershipNeverErrors/` | `[%dict%] contains %n%` → false no error |
| `Narrow_ChainWideBangBothBranches/` | `%config!file!path%` resolves on both branches |
| **Total** | **15** |

## Coverage cross-check (vs `architect/plan/test-coverage.md`)

Every row in the matrix maps to at least one test. Highlights:
- Stage 1 row → `Stage1_ComparisonEnumTests`
- Stage 2 rows (door, planes, GetParameter, navigation) → 4 files, 34 tests
- Stage 3 rows (references + path demolition) → 2 files, 30 tests + 3 goal tests (Cut2, Cut3, Cut6, Narrow_*)
- Stage 4 rows → 2 files, 21 tests + goal (Cut1)
- Stage 5 rows → 4 tests
- Stage 6 rows → 2 files, 19 tests + goal (Cut4, Cut5, Plane_Membership*, Failure_Sort*)
- Stage 7 rows → 2 files, 14 tests
- Failure matrix (9 rows) → all covered (gate rows in C#, others via Failure_* goal tests)
- Integration cuts (6) → 6 goal tests

## Notes for the coder

- **2–6 green unit:** Stages 2–6 land as one green unit. The Stage 6 demolition tests assert deleted types via reflection — they'll fail until the old mediator is gone.
- **Stage 7 gate ride:** tests in `Stage7_SurfaceGateTests` / `Stage7_PathGrowthTests` ride behind the warning-then-error gate; expect them to land last.
- **Read counter:** `Cut2.test.goal` + several C# tests assume `Data.MaterializeCount` exists as the read instrumentation (architect references it under "Existing surfaces" in `test-coverage.md` §3). Confirm wiring or add the counter as Stage 2 lands.
- **Reflection-based tests** (deletion assertions, gate probes) may need MetadataLoadContext or compile-time fixtures — coder's call which to use.
- **`!`-surface gate fixtures** (`Stage7_SurfaceGateTests.Gate_PublicItemSubtypeMember_ReturningString_FailsBuild`) need a temporary `.cs` fixture file that the gate flags; pattern follows the existing PLNG002 build-gate tests.
