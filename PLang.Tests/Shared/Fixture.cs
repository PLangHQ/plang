namespace PLang.Tests.Shared;

/// <summary>
/// A pinned fixture under the repo: frozen data a regression test compares the C# output against. An intended change
/// re-pins it through the test's own [Explicit] accept test, which writes the fixture from C#.
/// </summary>
public static class Fixture
{
    /// <summary>The repo's root: the folder holding PLang/app.</summary>
    public static string Root()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    /// <summary>The fixture at <paramref name="relative"/> (from the repo root), as a JSON node to read or re-pin.</summary>
    public static System.Text.Json.Nodes.JsonNode Read(string relative)
        => System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Root(), relative)))!;

    /// <summary>Re-pins the fixture at <paramref name="relative"/> with <paramref name="node"/>.</summary>
    public static void Write(string relative, System.Text.Json.Nodes.JsonNode node)
        => System.IO.File.WriteAllText(System.IO.Path.Combine(Root(), relative), node.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }) + "\n");
}
