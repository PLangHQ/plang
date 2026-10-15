using System.Collections.Generic;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Stage 2: a list of records / a dict written into an llm message renders as a readable markdown view
/// built from the primitive stream — columns with their (guessed) types, up to %!llm.setting.rows% rows,
/// then "--- N more rows ---". Writer-level and deterministic.
///
/// Column type is a guess from the scalar primitive a cell calls, so a value that writes as a String
/// (path/url/file/directory, choice, duration, size) is labelled "(text)" — see Table's doc (branch A
/// backlog: a writer learning a value's type without the wire envelope).
/// </summary>
public class TableWriterTests
{
    private static string Render(global::app.type.item.@this value, int rows,
        global::app.actor.context.@this ctx)
    {
        using var ms = new System.IO.MemoryStream();
        var w = new global::app.module.llm.Writer(ms, System.Text.Encoding.UTF8,
            global::app.type.item.culture.@this.Machine, length: 100_000, rows: rows);
        value.Output(w, global::app.View.Out, ctx).AsTask().GetAwaiter().GetResult();
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static global::app.type.item.@this Orders(int n, global::app.actor.context.@this ctx)
    {
        var rows = new List<object?>();
        for (int i = 1; i <= n; i++)
            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = i,
                ["total"] = 9.99m,
                ["date"] = new System.DateTime(2026, 1, 15),
            });
        return global::app.type.item.@this.Create(rows, ctx);
    }

    [Test]
    public async Task ListOfRecords_Over100_ColumnsTypes_100Rows_Marker()
    {
        await using var app = new PLangEngine("/test").Testing();
        var ctx = app.actor.list.User.Context;

        var md = Render(Orders(150, ctx), 100, ctx);

        // columns with their types (id/total guessed number, date datetime)
        await Assert.That(md).Contains("id (number)");
        await Assert.That(md).Contains("total (number)");
        await Assert.That(md).Contains("date (datetime)");
        // 100 data rows, then the marker for the 50 dropped
        await Assert.That(md).Contains("--- 50 more rows ---");
        // the 101st row's id (101) is not shown
        await Assert.That(md).DoesNotContain("| 101 |");
    }

    [Test]
    public async Task ListUnder100_AllRows_NoMarker()
    {
        await using var app = new PLangEngine("/test").Testing();
        var ctx = app.actor.list.User.Context;

        var md = Render(Orders(40, ctx), 100, ctx);

        await Assert.That(md).Contains("id (number)");
        await Assert.That(md).DoesNotContain("more rows");
        await Assert.That(md).Contains("| 40 |");   // the last row shows
    }

    [Test]
    public async Task RowsSetting_MovesTheCut()
    {
        await using var app = new PLangEngine("/test").Testing();
        var ctx = app.actor.list.User.Context;

        var md = Render(Orders(150, ctx), 10, ctx);

        await Assert.That(md).Contains("--- 140 more rows ---");
        await Assert.That(md).DoesNotContain("| 11 |");
    }

    [Test]
    public async Task Dict_KeysWithTypesAndValues()
    {
        await using var app = new PLangEngine("/test").Testing();
        var ctx = app.actor.list.User.Context;

        var cfg = global::app.type.item.@this.Create(new Dictionary<string, object?>
        {
            ["id"] = 7,
            ["name"] = "widget",
            ["active"] = true,
        }, ctx);

        var md = Render(cfg, 100, ctx);

        await Assert.That(md).Contains("id (number): 7");
        await Assert.That(md).Contains("name (text): widget");
        await Assert.That(md).Contains("active (bool): true");
    }
}
