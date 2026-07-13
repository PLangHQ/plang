namespace PLang.Tests.App.Types.ConversionGapTests;

using Data = global::app.data.@this;
using Text = global::app.type.item.text.@this;
using Number = global::app.type.item.number.@this;

/// <summary>
/// The bind-decline message: when a value cannot become the asked-for type, the
/// owning type's <c>Create</c> declines onto <c>data.Fail</c> with a readable
/// reason — names the target plang type, never leaks the raw CLR
/// "Object must implement IConvertible" text. (The unresolved-<c>%var%</c> cause is
/// reported one layer up, at variable resolution — see StartGoalTests.)
/// </summary>
public class CreateDeclineMessageTests
{
    private static readonly global::app.actor.context.@this Ctx = global::PLang.Tests.TestApp.SharedContext;

    [Test]
    public async Task Text_DeclinesOpaqueObject_NamesTextType_NoIConvertibleLeak()
    {
        var d = new Data("Message", (object?)null, context: Ctx);
        var result = Text.Create(new object(), d);

        await Assert.That(result).IsNull();
        await Assert.That(d.Error).IsNotNull();
        await Assert.That(d.Error!.Message).Contains("text");
        await Assert.That(d.Error!.Message).DoesNotContain("IConvertible");
    }

    [Test]
    public async Task Number_DeclinesNonNumericText_NamesNumberType()
    {
        var d = new Data("Count", (object?)null, context: Ctx);
        var result = Number.Create(new Text("not a number"), d);

        await Assert.That(result).IsNull();
        await Assert.That(d.Error).IsNotNull();
        await Assert.That(d.Error!.Message).Contains("number");
    }
}
