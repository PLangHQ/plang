using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// The new `table` type family (architect's 829785fbe revision). csv/xlsx
// → `{table, kind}`. A table is a grid (rows/columns/headers) — same
// shape, different encoding. The `(table, csv)` reader lands in-branch;
// `(table, xlsx)` is a follow-on. Until xlsx exists, a .xlsx still
// stamps `{table, xlsx}` and rides as raw bytes.
public class TableTypeTests
{
    // app/type/table/this.cs exists.
    [Test] public async Task TableType_Exists_AtAppTypeTable() { throw new System.NotImplementedException("not implemented"); }

    // app/type/table/serializer/Default.cs declares a static `Read` that
    // discovery indexes; the registry exposes the entry as
    // `(table, csv)` (and the `"*"` wildcard for raw-bytes passthrough
    // until specific kinds register).
    [Test] public async Task TableReader_Discovered_ForCsvKind() { throw new System.NotImplementedException("not implemented"); }

    // The follow-on contract — a .xlsx file still gets a stamp, and the
    // raw bytes survive. Stamping does not require a reader to exist; it
    // is a promise about shape.
    [Test] public async Task TableXlsx_StampsButHasNoReaderYet_RidesAsRawBytes() { throw new System.NotImplementedException("not implemented"); }

    // The shape claim — `table` advertises itself as a grid (rows,
    // columns, headers) regardless of source kind. csv-via-reader and
    // xlsx-via-reader both produce the same surface.
    [Test] public async Task Table_AdvertisesGridShape_RowsColumnsHeaders() { throw new System.NotImplementedException("not implemented"); }

    // Lazy: stamping `type=table` is not parsing. `_raw` is the csv text
    // until something navigates into it.
    [Test] public async Task TableCsv_StampingDoesNotParse_RawStaysCsvString() { throw new System.NotImplementedException("not implemented"); }

    // Navigation by shape: a `table` navigates by row/column. The exact
    // surface (e.g. `%t.rows`, `%t[0].name%`) is the coder's call —
    // open item; the contract here is that the access pattern is
    // row/column, not key lookup.
    [Test] public async Task TableNavigation_IsByRowAndColumn_NotByKey() { throw new System.NotImplementedException("not implemented"); }
}
