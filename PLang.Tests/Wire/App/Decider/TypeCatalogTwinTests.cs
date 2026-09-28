namespace PLang.Tests.App.Decider;

// os/system/builder/llm/types.json is the types as plang names them — each type's namespace (its identity), the word
// it goes by when its class declares one, and its aliases — for python (tools/decider params.py / build_pr.py) to
// name a C# type the way the builder does instead of guessing from a folder. This test writes the file from the
// types; a drift fails, with the file rewritten so the next run (and python) reads the types as they are.
public class TypeCatalogTwinTests
{
    [Test]
    public async Task TypesJson_IsTheTypes()
    {
        await using var app = TestApp.Create("/test");
        var types = new List<global::app.type.@this>();
        for (var i = 0; i < app.type.list.CountRaw; i++)
            types.Add((global::app.type.@this)app.type.list.At(i, app.actor.list.User.Context)!.Peek()!);

        var root = new System.Text.Json.Nodes.JsonObject();
        foreach (var type in types.Where(t => t.Namespace != null).OrderBy(t => t.Namespace, StringComparer.Ordinal))
        {
            var entry = new System.Text.Json.Nodes.JsonObject
            {
                ["word"] = type.Name == type.Namespace ? null : type.Name,
            };
            if (type.Alias.Count > 0) entry["alias"] = new System.Text.Json.Nodes.JsonArray(type.Alias.Select(a => (System.Text.Json.Nodes.JsonNode?)a).ToArray());
            root[type.Namespace!] = entry;
        }
        var written = root.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }) + "\n";

        var path = System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os", "system", "builder", "llm", "types.json");
        var held = System.IO.File.Exists(path) ? await System.IO.File.ReadAllTextAsync(path) : "";
        if (held != written) await System.IO.File.WriteAllTextAsync(path, written);
        await Assert.That(held).IsEqualTo(written).Because("types.json drifted from the types — rewritten; commit it");
    }
}
