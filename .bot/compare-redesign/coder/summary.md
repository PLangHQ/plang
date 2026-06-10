# Coder — compare-redesign

## Version: v6 (continuing the stage plan — no new review). Both suites GREEN.

- **Suites:** plang 322 pass / 0 fail / **0 stale** / 2 skipped; C# 4258/4267 — **0 real failures** (the 9 left are unfilled spec stubs, list below).

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

## Stage 3 + 7 progress (since the core landed)

- **Stage 3 COMPLETE:** all 30 C# stubs (ReferenceNarrow ×15, PathDemolition ×15) + all 5 plang cuts green. Along the way: chain-wide `!` segment split in ParseNextSegment ("!file!path"); interpolation (`Variable.Resolve`), dotted writes, `GetValue`, operator `contains`/`startswith`/`endswith`/`isempty`, assert Contains/NotContains all route through the door (references render content — the scalar contract); `directory` got ILoadable + `Contains(needle)` (membership over the listing); narrow headline = the CONTENT's family (parsed dict, not the channel's `item` stamp); IContext injection in GetChild Peeks for references (no read on the property plane).
- **Stage 7 underway:** PLNG003 gate live as WARNING (public instance member of an item.@this subtype returning raw CLR; ~200-finding worklist = the conversion backlog). `path` growth: `IsUnder`/`Matches`/`Kind` public typed members; `Absolute`/`Relative`/`Extension` now INTERNAL (interop inch; `!absolute`/`!relative`/`!extension` projections served by the NonPublic-aware `!` plane reflection); builder filter → `f.Matches(bf)`, read hint → `p.Kind`. Surface flips done: `text.Length`→number, `dict.Keys`→list<text> (+`KeyNames` internal raw twin), `FilePath.Size`/`file.Size`→number, `list.Count`/`dict.Count`→number (+`CountRaw` internal twins; number gained `<`/`<=`/`>`/`>=` ordering operators; 17 interior sites moved to CountRaw; list `count`/`last` handlers + dict/list navigator-count tests number-aware). All 10 SurfaceGate + 4 PathGrowth + 2 plane-resolver tests green.

## Remaining stubs (9 — all pinned to DEFERRED scope)

Born-typed-everything (Ingi explicitly deferred this to its own stage) + reserved-core protection:
AtSchemaBlocked_AsDictKey, BangReservedCore_Protected, DataType_Getter_NoCLRSniffing,
GenericToRaw_DoesNotExist_OnItemBase (item.ToRaw removal pending), NameField_RemovedFromEnvelope,
RawSlot_Dissolved, TextRawValue_IsPrivate (text.Value privatization — huge ripple),
Value_AuthoredScalar_ReturnsTypedNumber, VarReference_RidesAsTypedText.

## Next

1. Walk the PLNG003 worklist (the warning list IS the worklist) type by type; flip to error when clean. (~190 findings left — Goal/Step/Identity/StatInfo and friends, plus the value types' remaining raw members.)
2. Born-typed-everything stage (the 9 stubs above): authored scalars/var-refs ride as wrappers, text.Value private, item.ToRaw removal, reserved-core protection, _raw dissolution.
3. Known deferred: `%path!absolute%` Authorize gating on the `!` plane; the `!` NonPublic reflection arm is broad (any internal property is navigable) — tighten if codeanalyzer flags it.

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
