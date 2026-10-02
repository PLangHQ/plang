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

    [Test] public async Task ATemplateFile_IsTextsFormat_NotTheChoiceSetNamedTemplate()
    {
        await using var app = new global::app.@this("/tmp/tplkind-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;

        var type = app.type.list.Extension(".template", ctx);

        await Assert.That(type.Name).IsEqualTo("text");
        await Assert.That(type.kind.Name).IsEqualTo("template");
    }
}
