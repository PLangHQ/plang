namespace PLang.Tests.App.Types;

/// <summary>
/// Every member an os file navigates is one plang reads. A member off its class's member list reads nothing, and a
/// text holding it stays as written, so a miss is silent where it runs: this reads each reference in os/** whose root
/// says what it holds, hop by hop against each class's member list, and names each miss by file and line.
/// A reference whose root is the program's own variable (an item, a row) has no class to read it against here.
/// </summary>
public class OsReferenceTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/os-reference-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // What each root holds: the app, the error in play, the goal and step in play, and the builder's own %goal% and
    // %step% (the goal and step it builds).
    private static readonly Dictionary<string, System.Type> Root = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["!app"] = typeof(global::app.@this),
        ["!error"] = typeof(global::app.error.@this),
        ["!goal"] = typeof(global::app.goal.@this),
        ["!step"] = typeof(global::app.goal.step.@this),
        ["goal"] = typeof(global::app.goal.@this),
        ["step"] = typeof(global::app.goal.step.@this),
    };

    private static readonly string[] Extension = [".goal", ".llm", ".template", ".html", ".md", ".liquid"];

    // References to a member no class has, each waiting on its os file's own fix: error holds no retry count (the two
    // prompts are read by nothing), a goal's path is Path, and the app has no `system`.
    private static readonly HashSet<string> Stale =
    [
        "system/llm/fixGoalSystem.llm %!error.retryCount%",
        "system/llm/fixStepSystem.llm %!error.retryCount%",
        "system/modules/ui/llm/CreateTemplateFileSystem.llm %goal.RelativePath%",
        "system/modules/assert/isNull.examples.md %!app.system%",
    ];

    [Test] public async Task EveryMemberAnOsFileNavigates_IsOnItsClassesMemberList()
    {
        var os = System.IO.Path.Combine(Fixture.Root(), "os");
        var missed = new List<string>();
        var unrooted = 0;
        foreach (var file in System.IO.Directory.EnumerateFiles(os, "*", System.IO.SearchOption.AllDirectories)
                     .Where(f => Extension.Contains(System.IO.Path.GetExtension(f)) && !f.Contains($"{System.IO.Path.DirectorySeparatorChar}.build{System.IO.Path.DirectorySeparatorChar}")))
        {
            var lines = System.IO.File.ReadAllLines(file);
            for (int n = 0; n < lines.Length; n++)
                foreach (var variable in new global::app.type.item.variable.parser.@this(lines[n], _app.type.list).Variable)
                {
                    if (!Root.TryGetValue(variable.Code.Root.Name, out var clr)) { unrooted++; continue; }
                    var at = System.IO.Path.GetRelativePath(os, file).Replace(System.IO.Path.DirectorySeparatorChar, '/');
                    if (Miss(variable, clr) is { } miss && !Stale.Contains($"{at} {variable.Text}"))
                        missed.Add($"{at}:{n + 1} {variable.Text}: {miss}");
                }
        }

        await Assert.That(unrooted).IsGreaterThan(0);
        await Assert.That(missed).IsEmpty().Because(string.Join("\n", missed));
    }

    // The first member on the way its class doesn't list; null when every member it can follow is listed. It follows a
    // member while the member is a plang value read only through its member list: a value that navigates its own way
    // (a container, a registry, a node), an owner's settings and events (read by name), an index, a method or a
    // binding end what it can say.
    private string? Miss(global::app.type.item.variable.@this variable, System.Type clr)
    {
        foreach (var hop in variable.Code.Items().Skip(1))
        {
            if (hop is not global::app.type.item.variable.code.Property { IsBinding: false } property) return null;
            if (property.Name is "setting" or "on" || !ReadsByMemberList(clr)) return null;
            var member = _app.type.list.Member(clr)
                .FirstOrDefault(m => string.Equals(m.Name, property.Name, System.StringComparison.OrdinalIgnoreCase));
            if (member is null) return $"{clr.FullName} lists no member {property.Name}";
            var next = member.Member switch
            {
                System.Reflection.PropertyInfo p => p.PropertyType,
                System.Reflection.MethodInfo m => m.ReturnType,
                _ => null,
            };
            if (next is { IsGenericType: true } && next.GetGenericTypeDefinition() is var generic
                && (generic == typeof(System.Threading.Tasks.Task<>) || generic == typeof(System.Threading.Tasks.ValueTask<>)))
                next = next.GetGenericArguments()[0];
            if (next is null || !typeof(global::app.type.item.@this).IsAssignableFrom(next) || next.IsAbstract) return null;
            clr = next;
        }
        return null;
    }

    // whether a value of this class reads its members only as item does: its own list, then what it keeps
    private static bool ReadsByMemberList(System.Type clr)
        => clr.GetMethod("Get", [typeof(global::app.data.@this), typeof(string)])?.DeclaringType == typeof(global::app.type.item.@this);
}
