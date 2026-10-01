namespace PLang.Tests.App.Decider;

// os/system/builder/llm/settings.json is the classes of settings as the builder teaches them — each path's
// options, name, type and default — for python's prompt C (prompt_c.settings_block) to show the same Settings
// block the properties template does. This test writes the file from the C# classes; a drift fails, with the
// file rewritten so the next run (and python) reads the classes as they are.
public class SettingCatalogTwinTests
{
    [Test]
    public async Task SettingsJson_IsTheClassesOfSettings()
    {
        await using var app = new global::app.@this("/test").Testing();
        var context = app.actor.list.System.Context;
        var classes = app.type.list["setting"].kind.list(context).Items()
            .Select(t => t.kind).OfType<global::app.type.item.setting.kind.@this>()
            .OrderBy(k => k.Name, StringComparer.Ordinal);

        var root = new System.Text.Json.Nodes.JsonObject();
        foreach (var @class in classes)
        {
            var options = new System.Text.Json.Nodes.JsonArray();
            foreach (var p in @class.Property)
                options.Add(new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = p.Name,
                    ["type"] = p.Type.ToString(),
                    ["default"] = p.Default == null ? null : await Text(p.Default, context),
                });
            root[@class.Name] = options;
        }
        var written = root.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // list<text>, not list<text>
        }) + "\n";

        var path = System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os", "system", "builder", "llm", "settings.json");
        var held = System.IO.File.Exists(path) ? await System.IO.File.ReadAllTextAsync(path) : "";
        if (held != written) await System.IO.File.WriteAllTextAsync(path, written);
        await Assert.That(held).IsEqualTo(written).Because("settings.json drifted from the classes — rewritten; commit it");
    }

    // A default as the text format writes it — a leaf bare, a node or record as its json.
    private static async Task<string> Text(object value, global::app.actor.context.@this context)
    {
        using var text = new System.IO.MemoryStream();
        await context.App.type.list.Mime("text/plain").Encode(text, context.Ok(value), context);
        return System.Text.Encoding.UTF8.GetString(text.ToArray());
    }
}
