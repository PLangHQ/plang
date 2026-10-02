namespace PLang.Tests.App.Types;

// The template kinds: file.read's Template is a choice of them (plang), and the choice's set named template never
// answers for a .template file — that extension is text's own format.
public class TemplateKindTests
{
    [Test] public async Task AChoiceOfTemplateKind_IsPlang_AndRefusesAnyOther()
    {
        await using var app = new global::app.@this("/tmp/tplkind-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        global::app.data.@this<global::app.type.item.choice.@this<global::app.type.item.template.kind.@this>> Kind(string name)
            => new global::app.data.@this("Template", name, context: ctx).As<global::app.type.item.choice.@this<global::app.type.item.template.kind.@this>>();

        var plang = Kind("plang");
        var fluid = Kind("fluid");

        await Assert.That((await plang.Value())!.Value).IsTypeOf<global::app.type.item.template.kind.plang.@this>();
        await fluid.Value();
        await fluid.IsFailure();
        await Assert.That(fluid.Error!.Key).IsEqualTo("ChoiceInvalid");
    }

    // a row's template is its kind, by name: one that names no kind of template is refused at read, saying the kinds
    [Test] public async Task ARowsTemplate_NamingNoKind_IsRefusedAtRead()
    {
        await using var app = new global::app.@this("/tmp/tplkind-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        var row = "{\"name\":\"x\",\"type\":{\"name\":\"text\",\"template\":\"fluid\"},\"value\":\"hi %name%\"}";

        var refused = await Assert.That(async () => await ctx.App.type.list["wire"].kind["plang"]!.Decode(
            System.Text.Encoding.UTF8.GetBytes(row), ctx, view: global::app.View.Store)).Throws<global::app.error.DeclinedException>();
        var plang = await ctx.App.type.list["wire"].kind["plang"]!.Decode(System.Text.Encoding.UTF8.GetBytes(row.Replace("fluid", "plang")), ctx,
            view: global::app.View.Store);

        await Assert.That(refused!.Message).Contains("Valid: plang");
        await Assert.That(plang.Type.Template?.Name).IsEqualTo("plang");
    }

    [Test] public async Task ATemplateFile_IsTextsFormat_NotTheChoiceSetNamedTemplate()
    {
        await using var app = new global::app.@this("/tmp/tplkind-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;

        var type = app.type.list.Extension(".template", ctx);

        await Assert.That(type.Name).IsEqualTo("text");
        await Assert.That(type.kind.Name).IsEqualTo("template");
    }
}
