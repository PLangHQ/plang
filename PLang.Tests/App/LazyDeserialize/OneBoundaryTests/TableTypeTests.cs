using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;
using table = global::app.type.table.@this;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// The new `table` type family (architect's 829785fbe revision). csv/xlsx
// → `{table, kind}`. A table is a grid (rows/columns/headers) — same
// shape, different encoding. The `(table, csv)` reader lands in-branch;
// `(table, xlsx)` is a follow-on. Until xlsx exists, a .xlsx still
// stamps `{table, xlsx}` and rides as raw bytes.
public class TableTypeTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-table-" + System.Guid.NewGuid().ToString("N")[..8]));

    private const string Csv = "name,age\nAda,36\nGrace,40\n";

    // app/type/table/this.cs exists.
    [Test] public async Task TableType_Exists_AtAppTypeTable()
        => await Assert.That(typeof(table)).IsNotNull();

    // app/type/table/serializer/csv.cs declares a static `Read` that
    // discovery indexes; the registry exposes the entry as `(table, csv)`.
    // No wildcard `Default` reader: an unknown kind (xlsx) must fall through
    // to raw-bytes passthrough, not a wildcard parse that would fail.
    [Test] public async Task TableReader_Discovered_ForCsvKind()
    {
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("table", "csv")).IsNotNull();
    }

    // The follow-on contract — a .xlsx file still gets a stamp, and the
    // raw bytes survive. Stamping does not require a reader to exist; it
    // is a promise about shape. With no (table, xlsx) reader, Materialize
    // hands back the raw byte[] unchanged.
    [Test] public async Task TableXlsx_StampsButHasNoReaderYet_RidesAsRawBytes()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        await Assert.That(ctx.App.Type.Readers.Of("table", "xlsx")).IsNull();

        byte[] bytes = { 0x50, 0x4B, 0x03, 0x04 }; // PK.. zip header (xlsx is a zip)
        var d = data.FromRaw(bytes, type.Create("table", "xlsx", context: ctx), ctx, "sheet");
        await Assert.That(d.Value).IsEqualTo((object)bytes);
        await Assert.That(d.HasRaw).IsTrue();
    }

    // The shape claim — `table` advertises itself as a grid (rows,
    // columns, headers). The csv reader produces that surface.
    [Test] public async Task Table_AdvertisesGridShape_RowsColumnsHeaders()
    {
        var t = (table)global::app.type.table.serializer.csv.Read(Csv, "csv",
            new global::app.type.reader.ReadContext(null))!;
        await Assert.That(t.Headers.Count).IsEqualTo(2);
        await Assert.That(t.ColumnCount).IsEqualTo(2);
        await Assert.That(t.RowCount).IsEqualTo(2);
        await Assert.That(t.Headers[0]).IsEqualTo("name");
    }

    // Lazy: stamping `type=table` is not parsing. `_raw` is the csv text
    // until something navigates into it.
    [Test] public async Task TableCsv_StampingDoesNotParse_RawStaysCsvString()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw(Csv, type.Create("table", "csv", context: ctx), ctx, "t");
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
        await Assert.That(d.Raw).IsEqualTo((object)Csv);
    }

    // Navigation by shape: a `table` navigates by row then column —
    // `%t.rows[0].name%`, NOT a flat key lookup `%t.name%`. The chosen
    // surface exposes Rows (each keyed by header) and Headers.
    [Test] public async Task TableNavigation_IsByRowAndColumn_NotByKey()
    {
        var t = (table)global::app.type.table.serializer.csv.Read(Csv, "csv",
            new global::app.type.reader.ReadContext(null))!;
        await Assert.That(t.Rows[0]["name"]).IsEqualTo((object?)"Ada");
        await Assert.That(t.Rows[1]["age"]).IsEqualTo((object?)"40");
    }
}
