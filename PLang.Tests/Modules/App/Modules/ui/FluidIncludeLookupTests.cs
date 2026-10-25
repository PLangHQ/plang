namespace PLang.Tests.App.Modules.ui;

/// <summary>Fluid's include lookup: a literal include is read through the gate, a name by a variable is refused, and
/// a <c>..</c> in a name gets no further than the gate lets it (security, packages).</summary>
public class FluidIncludeLookupTests
{
    private sealed class Canned : global::app.channel.@this
    {
        public int Asked;
        private readonly global::app.actor.context.@this _ctx;
        public Canned(global::app.actor.context.@this ctx) { _ctx = ctx; Name = "input"; Direction = global::app.channel.ChannelDirection.Bidirectional; }
        public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default) => Task.FromResult(_ctx.Ok());
        public override Task<global::app.data.@this> Read(CancellationToken ct = default) => Task.FromResult(_ctx.Ok((object?)null));
        public override Task<global::app.data.@this> Ask(global::app.module.output.ask action, CancellationToken ct = default)
        { Asked++; return Task.FromResult(_ctx.Ok("n")); }
    }

    private static async Task<(global::app.data.@this result, Canned asking, string root, string foreign)> Render(string template)
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-pk-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        var foreign = System.IO.Directory.CreateDirectory(root + "-foreign").FullName;
        System.IO.File.WriteAllText(System.IO.Path.Combine(foreign, "secret.liquid"), "SECRET_TOKEN");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "p"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "p", "ok.liquid"), "IN_ROOT");
        var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var asking = new Canned(user.Context);
        user.Channel.Register(asking);
        var goal = new Goal(app.type.list) { Name = "Host", Path = global::app.type.item.path.@this.Resolve("/host.goal", user.Context) };
        await using var inGoal = user.Context.call.Push(goal, await user.Context.call.Setting());
        var action = new global::app.module.ui.Render(user.Context)
        {
            Template = new global::app.data.@this<global::app.type.item.text.@this>("Template", template, context: user.Context),
            IsFile = new global::app.data.@this<global::app.type.item.@bool.@this>("IsFile", false, context: user.Context),
        };
        var result = await new global::app.module.ui.code.Fluid().Render(action);
        return (result, asking, root, foreign);
    }

    private static async Task<string> Text(global::app.data.@this r) => r.Success ? (await r.Value())?.ToString() ?? "" : r.Error?.Message ?? "";

    [Test]
    public async Task ADotDotName_ReachesNoFurtherThanTheGate()
    {
        // each render makes its own root and its sibling <root>-foreign holding secret.liquid; the name is learned from a
        // first render, then used against a second app whose root has the same shape
        var first = await Render("{% include 'p/ok' %}");
        var foreignName = System.IO.Path.GetFileName(first.foreign);
        var r = await Render("{% include '../" + foreignName + "/secret' %}");
        System.IO.File.WriteAllText(System.IO.Path.Combine(first.foreign, "secret.liquid"), "SECRET_TOKEN");
        var sibling = await Render("{% include '../../" + System.IO.Path.GetFileName(System.IO.Path.GetTempPath().TrimEnd('/')) + "/" + foreignName + "/secret' %}");
        await Assert.That(await Text(r.result)).DoesNotContain("SECRET_TOKEN");
        await Assert.That(await Text(sibling.result)).DoesNotContain("SECRET_TOKEN");
        await Assert.That(await Text((await Render("{% include '../../../../../../../tmp/" + foreignName + "/secret' %}")).result)).DoesNotContain("SECRET_TOKEN");
    }

    [Test]
    public async Task ANameByAVariable_IsRefused_EvenWhenItNamesAnInRootFile()
    {
        var r = await Render("{% assign n = 'p/ok' %}{% include n %}");
        await Assert.That(r.result.Success).IsFalse();
        await Assert.That(r.result.Error!.Key).IsEqualTo("IncludeNotLiteral");
    }

    [Test]
    public async Task ALiteralInRootName_Renders_WithoutAnAsk()
    {
        var r = await Render("{% include 'p/ok' %}");
        await Assert.That(await Text(r.result)).Contains("IN_ROOT");
        await Assert.That(r.asking.Asked).IsEqualTo(0);
    }

    // a variable that spells a DIFFERENT path to a literally included file (./ or case) is still refused
    [Test]
    public async Task AVariableSpellingAnotherPathToTheSameFile_IsRefused()
    {
        var r = await Render("{% include 'p/ok' %}{% assign n = './p/ok' %}{% include n %}");
        await Assert.That(r.result.Success).IsFalse();
    }
}
