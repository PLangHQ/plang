namespace app.module.test;

/// <summary>
/// The tests under a folder: every <c>*.test.goal</c> file, a fresh one as its test, a stale one naming why
/// (<see cref="app.test.list.@this.Discover"/> — discovery is the test list's). Returns a list&lt;test&gt;
/// that test.start consumes.
/// </summary>
[Action("discover")]
public partial class discover : IContext
{
    /// <summary>Directory to walk. AuthGate(Read) enforces in-root vs prompt-or-deny.</summary>
    [Default(".")]
    public partial data.@this<global::app.type.item.path.@this> Path { get; init; }

    /// <summary>Filename pattern. Default matches PLang test convention.</summary>
    [Default("*.test.goal")]
    public partial data.@this<global::app.type.item.text.@this> Pattern { get; init; }

    /// <summary>Whether the tests in subfolders are found too — included, unless the step skips them.</summary>
    [Default(global::app.module.file.type.subfolder.include)]
    public partial data.@this<global::app.type.item.choice.@this<global::app.module.file.type.subfolder>> Subfolder { get; init; }

    public async Task<data.@this<global::app.type.item.list.@this<global::app.test.@this>>> Start()
        => data.@this<global::app.type.item.list.@this<global::app.test.@this>>.From(
            await Path.Use(root => Context.App.test.list.Discover(root, Pattern, Subfolder, Context)));
}
