# Test plan — collections-are-data

Approved batch plan covering Stages 1–6. Test bodies are skeletons (`Assert.Fail("Not implemented")` for C#, `- throw "not implemented"` for PLang). Coder implements behavior to make them pass.

## Batches

| Batch | Stage | Layer | Tests | File |
|---|---|---|---|---|
| 1 | 1 | C# unit | 11 | `PLang.Tests/App/CollectionsAreData/Stage1_DictValueTypeTests.cs` |
| 2 | 1 | C# unit | 4 | `PLang.Tests/App/CollectionsAreData/Stage1_DictNavigationAndWriterTests.cs` |
| 2 | 1 | PLang | 4 | `Tests/CollectionsAreData/Stage1/Test*.test.goal` |
| 3 | 2 | C# unit | 4 | `PLang.Tests/App/CollectionsAreData/Stage2_SetRebindTests.cs` |
| 3 | 2 | PLang | 3 | `Tests/CollectionsAreData/Stage2/Test*.test.goal` |
| 4 | 3 | C# unit | 10 | `PLang.Tests/App/CollectionsAreData/Stage3_ArraysAsDataTests.cs` |
| 5 | 3 | PLang | 6 | `Tests/CollectionsAreData/Stage3/Test*.test.goal` + 2 already in `Tests/LazyDeserialize/` |
| 6 | 4 | C# unit | 10 | `PLang.Tests/App/CollectionsAreData/Stage4_TypedCompareTests.cs` |
| 7 | 5 | PLang | 7 | `Tests/CollectionsAreData/Stage5/Test*.test.goal` |
| 8 | 6 | C# unit + PLang | 1 + 4 | `PLang.Tests/App/CollectionsAreData/Stage6_ItemApexTests.cs` + `Tests/CollectionsAreData/Stage6/Test*.test.goal` |

**Total: ~64 tests.**

## Layer split rationale

- **C# unit** pins the internal shape: new value types (`dict.@this`, `list.@this`), navigator collapse, writer type-discrimination, `Variables.Set` rebind plumbing, typed-compare adapter.
- **PLang integration** pins developer behavior: literal navigation, sign→add→verify, `where`/`sort`/`group`, `is` queries, file round-trips. F1's exact gap lives here — the writer alone won't catch it.

## Already shipped (failing-test contracts)

- `Tests/LazyDeserialize/SignedDataSurvivesInList.test.goal` — pre-existing in-memory `add` path. Passes today (add.cs already shallow-clones); kept as regression guard.
- `Tests/LazyDeserialize/SignedListSurvivesJsonRoundTrip.test.goal` — parse-seam round-trip. Skeleton form.

## What the test-designer does NOT write

Per `characters/test-designer/character.md`: signatures only. Bodies are `Assert.Fail("Not implemented")` for C# and `- throw "not implemented"` for PLang. No `plang build`, no `dotnet run`. Coder implements and reports back.

## Compare contract (Stage 4)

Settled by Ingi (architect commit `155712afb`):
- within a type: natural order (number numeric incl. kind widening, datetime chronological, duration by length, text lexical)
- nulls sort last
- ordering two genuinely different value types throws "cannot order X against Y"
- orderable: `number`, `datetime`, `duration`, `text`
- equality-only: `dict`, `list`, `bool`, `table`, `null`
- existing `if`-path coercions (numeric widening, string↔number) preserved
