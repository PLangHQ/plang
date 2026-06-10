# Coder — compare-redesign

## Version: v6 (continuing the stage plan — no new review). Both suites GREEN.

- **Suites:** plang 317 pass / 0 fail / 5 stale (Stage-3/7 stubs) / 2 skipped; C# 4217/4267 — **0 real failures** (the 50 are unfilled CompareRedesign Stage-3/7 spec stubs).

## What this is

The compare-redesign branch (architect plan in `.bot/compare-redesign/architect/`): a sign-free `Comparison` enum, per-type comparison hooks, `data.Compare` as THE comparison entry, consumer boundary mapping, and (Stage 3) `file`/`directory`/`url` reference types with narrow-on-examination.

## State by stage

- **Stages 1–2, 2.1, 4, 5, 6 — DONE and green.** Per-type `CompareRank`/`Compare` hooks (text 10 … list 75), `Data.Compare` (rank → null policy → driver), boundary table in operators/assert/sort/list-ops, old mediator demolished (`app.data.Compare`, `ScalarComparer`, `IEquatableValue`/`IOrderableValue`, `NormalizeTypes`), `Compare`→`Diff` rename, Pile-2 decompose sites converted (sqlite typed door, llm cache dict navigation, identity dict→json→STJ, GoalCall slot accessor — no recursive `ToRaw` callers left except CommandLineParser, documented infra perimeter).
- **Stage 3 — core landed, green; stub fills remain.**
  - Path demolition: `Content`/`Source` removed; `_absolutePath` → private `_location` (as-typed) + cached `_absolute` primed at ctor (anchor-at-resolve-time preserved); `Raw` is now a view over `_location`; ToString/`path.Write(IWriter)` emit the **portable form** (as-typed verbatim; internally-derived paths collapse to root-relative so the install root never leaks).
  - New types: `app/type/file`, `app/type/directory`, `app/type/url` (+ `serializer/Default` renderers). Lattice via static `Type` list (file is-a path), like image.
  - `file.read` → returns a REFERENCE (stat at read time for NotFound; content lazy). Eager arms: `.pr` plang container (GoalCall.LoadFromFile depends on it), image, ResolveVariables.
  - **Semantics line (important):** scalar use of a reference yields **raw content** through the door (`Value()` → `ScalarContent`, bytes cached on the reference); only **examination** — navigation (`%x.field%`, except `.Type`) and `is <type>` — parses + narrows (`Data.NarrowReference`: in-place `_value`/`_type` mutation, prior retained via `type.Accumulate`, location-only reference stashed in `Properties[name]` for chain-wide `!`).
  - Type chain: `type.@this.List` (headline-first, `[JsonIgnore]` — self-inclusive), `Accumulate`, `Facet(name)`; `Is` consults priors. `!` resolver: chain-facet arm + value-property fallback (`!path`/`!host`/`!size` via Peek, no read).
  - Registry fix: an `@this` deriving from another family's `@this` (FilePath : path.@this) resolves to the FAMILY name ("path") and never claims the name slot — fixes the `app.type.file` vs `app.type.path.file` "file" collision that mis-dispatched renderers/signing. `Reaches` is assignability-aware (the "path" entity may carry a variant CLR mate).
  - Consumers moved to the door (raw content for references): `Variable.GetValue`, operator `contains`/`startswith`/`endswith`/`isempty`, assert `Contains`/`NotContains`; `variable.set` keeps a reference AS the value (no door on store); cache wrap `LoadAsync`s ILoadable before SetAsync so hits serve memory; `type.Compare` only lets a hook-bearing name-keyed family drive (hookless `file` defers to the content's family).
  - Build hint (`file.read.Build`) stamps `{file, <ext>}` (image keeps `{image, ext}`).

## Test-contract updates (flag for Ingi)

Stage 3 inverts `read` (reference headline) per the approved architect spec; these pinned the old stamp and were updated accordingly:
- `Tests/LazyDeserialize/ReadConfigJson_UntouchedIsJsonString.test.goal` — `Type.Name` "item"→"file" (kind json; raw-scalar assert unchanged, passes).
- `Tests/LazyDeserialize/ReadCsv_LandsAsTable.test.goal` — `Type.Name` "table"→"file" (kind csv).
- C#: `FileRead_Build_*` (table/item→file), `FileType_MimeType_FromExtension` (text→file), `BuilderValidate_BuildInferenceWinsOverDefaultObject` (table→file), `ConditionIfBranchIndexTests` fixture (`new object()`→dict, per its own comment).

## Next

1. Fill `Stage3_ReferenceNarrowTests` (15 stubs) + remaining `Stage3_PathDemolitionTests` (9 of 15 left) — machinery now exists for most.
2. Fill the 5 stale plang stubs: Cut2_LazyReadAndNarrow, Cut3_WriteOutDirIsListing, Cut4_SortByIoKey, Cut6_ReadThenScalarYieldsContent, Narrow_ChainWideBangBothBranches (+ SortBySize C# stub).
3. Stage 7 (surface typing) per stage-7-surface-typing.md; Stage7_PathGrowth/SurfaceGate stubs.
4. Known deferred: `%path!absolute%` Authorize gating (PathBangAbsolute stub), `!extension` serialisation question (wire is a single location string now), directory write-out needs a Load() pass that materialises the listing.

## Code example (the narrow seam)

```csharp
// data/this.cs — scalar use = content; examination = narrow
public virtual ValueTask<object?> Value()
{
    var v = Materialize();
    if (v is global::app.type.file.@this or global::app.type.url.@this)
        return ScalarContent(v);              // raw bytes/text, no type change
    return new(v);
}
// this.Navigation.cs — GetChildValue head
if (!key.Equals("Type", ...) && Peek() is file.@this or url.@this)
    await NarrowReference(Peek()!);           // parse + in-place type chain
```
