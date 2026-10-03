namespace PLang.Tests.App.Serialization;

// A separator is a named one (line, comma, tab, space, semicolon) standing for its characters, or any characters as
// written. The decider is offered the named ones by name; a .pr carries the name; a text that is exactly a name is
// that separator, from a literal and from a variable alike.
public class SeparatorTests
{
    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    private static global::app.@this Os() => new global::app.@this(System.IO.Path.Combine(RepoRoot(), "os")).Testing().Building();

    // each offer as the formal line writes it, and as the decider is shown it
    private static (List<string> Written, List<string> Shown) Offered(IReadOnlyList<global::app.type.item.@this> offers, global::app.actor.context.@this context)
    {
        var written = offers.Select(offer =>
        {
            var writer = new global::app.goal.step.action.formal.Writer();
            offer.Write(writer);
            return writer.ToString();
        }).ToList();
        var shown = offers.Select(offer =>
        {
            using var stream = new System.IO.MemoryStream();
            offer.Write(new global::app.type.item.text.Writer(stream, System.Text.Encoding.UTF8, context.Setting.Of<global::app.setting.@this>().Culture));
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }).ToList();
        return (written, shown);
    }

    [Test]
    public async Task SplitIntoLines_IsOfferedTheNamedSeparatorsByName_AndTheStepsVariables()
    {
        await using var os = Os();
        var context = os.actor.list.User.Context;
        var step = Make.Goal(context, "G", "/G.goal", Make.Step("split %x% into lines", 0)).Step[0];

        var (written, shown) = Offered(await os.Module("list")["split"]!["Separator"]!.Type.Offers(step), context);

        await Assert.That(shown).Contains("line");
        await Assert.That(shown).Contains("comma");
        await Assert.That(written).Contains("\"line\"");
        await Assert.That(written).Contains("%x%");
        await Assert.That(shown.Any(text => text.Contains('\n'))).IsFalse();
    }

    // the formal line read into a step, saved as a .pr and loaded, and its Separator read back
    private static async Task<global::app.type.item.separator.@this> RoundTrip(string separator)
    {
        await using var os = Os();
        var context = os.actor.list.User.Context;
        var goal = global::app.goal.@this.Parse("Start\n- split %x%\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;
        var read = new global::app.goal.step.action.formal.Reader(goal.Step[0], context.App.module.list)
            .Read($"list.split(Value=%x%, Separator={separator})", context);
        await read.IsSuccess();
        goal.Step[0].Code = (global::app.goal.step.action.list.@this)read.Peek()!;

        var pr = await context.Pr(goal);
        var loaded = await RealGoalLoad.Read(os, pr);
        return (global::app.type.item.separator.@this)(await loaded.Step[0].Code[0].Property["Separator"]!.Data(context).Value())!;
    }

    [Test]
    public async Task ANamedSeparator_RidesThePrByName_ALiteralByItsCharacters()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var line = await RoundTrip("\"line\"");
        var commaSpace = await RoundTrip("\", \"");
        var newline = await RoundTrip("\"\\n\"");

        await Assert.That(line.Characters).IsEqualTo("\n");
        await Assert.That(Written(line, ctx)).IsEqualTo("line");
        await Assert.That(commaSpace.Characters).IsEqualTo(", ");
        await Assert.That(Written(commaSpace, ctx)).IsEqualTo(", ");
        await Assert.That(newline.Characters).IsEqualTo("\n");
        await Assert.That(Written(newline, ctx)).IsEqualTo("\n");
    }

    private static string Written(global::app.type.item.separator.@this separator, global::app.actor.context.@this context)
    {
        using var stream = new System.IO.MemoryStream();
        separator.Write(new global::app.type.item.text.Writer(stream, System.Text.Encoding.UTF8, context.Setting.Of<global::app.setting.@this>().Culture));
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // what a program does with them: split by line, join with comma, a literal as itself, a variable naming one
    private static async Task<global::app.type.item.@this?> Ran(global::app.@this app, params Make.StepDef[] steps)
    {
        var context = app.actor.list.User.Context;
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(context, "Start", "/Start.goal", steps));
        await (await goal.Start(context)).IsSuccess();
        return await (await context.Variable.Get("r")).Value();
    }

    [Test]
    public async Task SplitByLine_JoinWithComma_ALiteralItself_AndAVariableNamingOne()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        global::app.goal.step.action.@this Keep(string name) => Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", name, "variable"), ("Value", "%!data%"));

        var lines = await Ran(app, Make.Step("split", Make.Action(ctx, "list", "split", ("Value", "a\nb"), ("Separator", "line")), Keep("r")));
        await Assert.That(lines?.ToString()).IsEqualTo("[a, b]");

        await ctx.Variable.Set("items", new List<object?> { "a", "b", "c" });
        var joined = await Ran(app, Make.Step("join", Make.Action(ctx, "list", "join", Make.Param(ctx, "ListName", "items", "variable"), ("Separator", "comma")), Keep("r")));
        await Assert.That(joined?.ToString()).IsEqualTo("a,b,c");

        var piped = await Ran(app, Make.Step("join", Make.Action(ctx, "list", "join", Make.Param(ctx, "ListName", "items", "variable"), ("Separator", " | ")), Keep("r")));
        await Assert.That(piped?.ToString()).IsEqualTo("a | b | c");

        await ctx.Variable.Set("sep", "comma");
        var named = await Ran(app, Make.Step("split", Make.Action(ctx, "list", "split", ("Value", "x,y"), ("Separator", "%sep%")), Keep("r")));
        await Assert.That(named?.ToString()).IsEqualTo("[x, y]");
    }
}
