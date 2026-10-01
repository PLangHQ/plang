namespace PLang.Tests.App.actions.list;

// The builder's pick for words → list.query, pinned against the committed golden .pr under
// test/plan/list-query/module/list/query/build/. These catch a later rebuild that picks an old
// action (list.sort/where/group) — which the plang run can miss because sort still sorts. The
// pin goes red against a .pr built before the list.query teaching (that .pr holds list.sort etc.).
public class QueryBuildPinTests
{
    private static (string Pick, string Query) QueryStep(string prName)
    {
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "test", "plan", "list-query")))
            dir = dir.Parent;
        var path = System.IO.Path.Combine(dir!.FullName, "test", "plan", "list-query",
            "module", "list", "query", "build", ".build", prName);
        var pr = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path)).RootElement;
        foreach (var step in pr.GetProperty("step").EnumerateArray())
        {
            if (!step.TryGetProperty("code", out var code)) continue;
            foreach (var a in code.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString()!;
                if (name is "query" or "sort" or "where" or "group" or "unique")
                {
                    var q = "";
                    if (a.TryGetProperty("property", out var props))
                        foreach (var p in props.EnumerateArray())
                            if (p.GetProperty("name").GetString() == "Query")
                                q = p.GetProperty("value").GetRawText();
                    return ($"{a.GetProperty("module").GetString()}.{name}", q);
                }
            }
        }
        return ("", "");
    }

    [Test] public async Task OnePart_PicksListQuery_WithAWhere()
    {
        var (pick, q) = QueryStep("onepart.test.pr");
        await Assert.That(pick).IsEqualTo("list.query");
        await Assert.That(q).Contains("\"where\"");
        await Assert.That(q).Contains("\"age\"");
    }

    [Test] public async Task ManyPart_PicksListQuery_WithEveryPart()
    {
        var (pick, q) = QueryStep("manypart.test.pr");
        await Assert.That(pick).IsEqualTo("list.query");
        await Assert.That(q).Contains("\"and\"");
        await Assert.That(q).Contains("\"group\"");
        await Assert.That(q).Contains("\"distinct\"");
        await Assert.That(q).Contains("\"order\"");
    }

    [Test] public async Task HighestFirst_PicksListQuery_WithOrderDesc()
    {
        var (pick, q) = QueryStep("highestfirst.test.pr");
        await Assert.That(pick).IsEqualTo("list.query");
        await Assert.That(q.Replace(" ", "")).Contains("\"desc\":true");
    }
}
