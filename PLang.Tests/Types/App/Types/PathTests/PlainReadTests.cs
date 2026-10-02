using PLang.Tests.Shared;

namespace PLang.Tests.App.Types.PathTests;

// A plain file.read (no load vars) hands the file's content as it is: a %name% or %!x% in it is text, never filled —
// not where it is read, not when it is set to a variable, not when a later step reads that variable.
public class PlainReadTests
{
    private static async Task<(global::app.@this app, global::app.actor.context.@this ctx)> NewApp(string content)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plainread-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "greeting.txt"), content);
        var app = new global::app.@this(root).Testing();
        return (app, app.actor.list.User.Context);
    }

    private static global::app.goal.step.action.@this Set(global::app.actor.context.@this ctx, string name, object? value)
        => Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", name, "variable"), ("Value", value));

    private static async Task<global::app.data.@this> Run(global::app.@this app, global::app.actor.context.@this ctx, params Make.StepDef[] more)
    {
        var steps = new List<Make.StepDef>
        {
            Make.Step("set name", Set(ctx, "name", "World")),
            Make.Step("read greeting.txt", Make.Action(ctx, "file", "read",
                Make.Param(ctx, "Path", "greeting.txt", "path"))),
            Make.Step("set raw", Set(ctx, "raw", "%!data%")),
        };
        steps.AddRange(more);
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "G", "/g.goal", steps.ToArray()));
        return await goal.Start(ctx);
    }

    [Test] public async Task APlainRead_SetToAVariable_StaysLiteral()
    {
        var (app, ctx) = await NewApp("Hello %name%!");
        await using var _ = app;

        await (await Run(app, ctx, Make.Step("set copy", Set(ctx, "copy", "%raw%")))).IsSuccess();

        await Assert.That((await (await ctx.Variable.Get("raw")).Value())?.ToString()).IsEqualTo("Hello %name%!");
        await Assert.That((await (await ctx.Variable.Get("copy")).Value())?.ToString()).IsEqualTo("Hello %name%!");
    }

    [Test] public async Task APlainRead_AssertedNotToContain_ReadsLiteral()
    {
        var (app, ctx) = await NewApp("Hello %name%!");
        await using var _ = app;

        var ran = await Run(app, ctx, Make.Step("assert raw does not contain World",
            Make.Action(ctx, "assert", "notContains", ("Value", "World"), ("Container", "%raw%"))));

        await ran.IsSuccess();
    }

    // the same file read with its variables first, then plain: the plain read is its own, never the filled one
    [Test] public async Task APlainRead_AfterALoadVarsReadOfTheSameFile_StaysLiteral()
    {
        var (app, ctx) = await NewApp("Hello %name%!");
        await using var _ = app;
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "G", "/g.goal",
            Make.Step("set name", Set(ctx, "name", "World")),
            Make.Step("read greeting.txt, load vars", Make.Action(ctx, "file", "read",
                Make.Param(ctx, "Path", "greeting.txt", "path"), ("Template", "plang"))),
            Make.Step("set filled", Set(ctx, "filled", "%!data%")),
            Make.Step("read greeting.txt", Make.Action(ctx, "file", "read",
                Make.Param(ctx, "Path", "greeting.txt", "path"))),
            Make.Step("set raw", Set(ctx, "raw", "%!data%")),
            Make.Step("assert raw does not contain World",
                Make.Action(ctx, "assert", "notContains", ("Value", "World"), ("Container", "%raw%"))),
            Make.Step("assert filled contains World",
                Make.Action(ctx, "assert", "contains", ("Value", "World"), ("Container", "%filled%")))));

        await (await goal.Start(ctx)).IsSuccess();

        await Assert.That((await (await ctx.Variable.Get("raw")).Value())?.ToString()).IsEqualTo("Hello %name%!");
    }

    // as the builder writes `read 'x', write to %raw%`: the set holds the read as a file (Type=file); the assert
    // asks whether %raw% (the container) holds "World" (the value)
    [Test] public async Task APlainRead_HeldAsAFile_AfterALoadVarsRead_StaysLiteral()
    {
        var (app, ctx) = await NewApp("Hello %name%!");
        await using var _ = app;
        global::app.goal.step.action.@this SetFile(string name)
            => Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", name, "variable"),
                Make.Param(ctx, "Value", "%!data%", new global::app.type.@this("item", template: new global::app.type.item.template.kind.plang.@this())),
                Make.Param(ctx, "Type", "file", "type"));
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "G", "/g.goal",
            Make.Step("set name", Set(ctx, "name", "World")),
            Make.Step("read greeting.txt, load vars", Make.Action(ctx, "file", "read",
                Make.Param(ctx, "Path", "greeting.txt", "path"), ("Template", "plang")), SetFile("filled")),
            Make.Step("read greeting.txt", Make.Action(ctx, "file", "read",
                Make.Param(ctx, "Path", "greeting.txt", "path")), SetFile("raw")),
            Make.Step("write out %raw%", Make.Action(ctx, "output", "write",
                Make.Param(ctx, "Data", "%raw%", new global::app.type.@this("item", template: new global::app.type.item.template.kind.plang.@this())))),
            Make.Step("assert that %raw% does not contain World", Make.Action(ctx, "assert", "notContains",
                ("Value", "World"), Make.Param(ctx, "Container", "%raw%", new global::app.type.@this("item", template: new global::app.type.item.template.kind.plang.@this()))))));

        await (await goal.Start(ctx)).IsSuccess();

        await Assert.That((await (await ctx.Variable.Get("filled")).Value())?.ToString()).IsEqualTo("Hello World!");
        await Assert.That((await (await ctx.Variable.Get("raw")).Value())?.ToString()).IsEqualTo("Hello %name%!");
    }

    [Test] public async Task APlainRead_HoldingAnAppVariable_StaysLiteral()
    {
        var (app, ctx) = await NewApp("count %!app.type.list.count%");
        await using var _ = app;

        await (await Run(app, ctx, Make.Step("set copy", Set(ctx, "copy", "%raw%")))).IsSuccess();

        await Assert.That((await (await ctx.Variable.Get("copy")).Value())?.ToString()).IsEqualTo("count %!app.type.list.count%");
    }
}
