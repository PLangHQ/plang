namespace PLang.Tests.App.Modules.variable;

// A template renders when it is read. A variable bound to a template that names the variable itself
// (`set %label% = '%label%-and-done'`) reaches itself while it renders — the program's cycle error.
public class TemplateCycleTests
{
    [Test]
    public async Task VariableHoldingATemplateThatNamesIt_IsAResolveCycle()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cycle-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;
        var template = app.type.list[new global::app.type.@this("text", (string?)null, template: "plang"), ctx];
        var born = await template.Create("%label%-and-done", ctx, "label");
        await born.IsSuccess();
        await ctx.Variable.Set("label", born);

        var read = await ctx.Variable.Get("label");
        string? key;
        try { await read!.Value(); key = read.Error?.Key; }
        catch (global::app.error.AppException ex) { key = ex.Error.Key; }

        await Assert.That(key).IsEqualTo("VarResolveCycle");
    }
}
