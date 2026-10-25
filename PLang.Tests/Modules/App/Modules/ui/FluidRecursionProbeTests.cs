namespace PLang.Tests.App.Modules.ui;

/// <summary>Security probe (bugs pass-out): a template that includes itself, or two that include each other, ends in an
/// error — never a hang or a crash. Run on a thread with a bound so a hang is a red test, not a stuck run.</summary>
public class FluidRecursionProbeTests
{
    private static async Task<string> Page(params (string file, string content)[] files)
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-fr-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        foreach (var (file, content) in files) System.IO.File.WriteAllText(System.IO.Path.Combine(root, file), content);
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var goal = new Goal(app.type.list) { Name = "Host", Path = global::app.type.item.path.@this.Resolve("/host.goal", user.Context) };
        await using var inGoal = user.Context.call.Push(goal, await user.Context.call.Setting());
        var action = new global::app.module.ui.Render(user.Context)
        {
            Template = new global::app.data.@this<global::app.type.item.text.@this>("Template", "/" + files[0].file, context: user.Context),
            IsFile = new global::app.data.@this<global::app.type.item.@bool.@this>("IsFile", true, context: user.Context),
        };
        var r = await new global::app.module.ui.code.Fluid().Render(action);
        return r.Success ? "OK" : "ERR:" + r.Error?.Key;
    }

    private static async Task<string> Bounded(Func<Task<string>> run)
    {
        var task = Task.Run(run);
        return await Task.WhenAny(task, Task.Delay(15000)) == task ? await task : "HANG";
    }

    [Test]
    public async Task ATemplateThatIncludesItself_EndsInAnError()
    {
        var o = await Bounded(() => Page(("a.liquid", "x{% include 'a' %}")));
        await Assert.That(o).StartsWith("ERR:");
    }

    [Test]
    public async Task TwoTemplatesThatIncludeEachOther_EndInAnError()
    {
        var o = await Bounded(() => Page(("a.liquid", "a{% include 'b' %}"), ("b.liquid", "b{% include 'a' %}")));
        await Assert.That(o).StartsWith("ERR:");
    }
}
