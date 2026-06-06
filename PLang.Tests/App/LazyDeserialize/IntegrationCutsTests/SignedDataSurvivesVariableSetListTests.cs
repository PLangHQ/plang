using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.module.signing;
using data = global::app.data.@this;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Twin of the `SignedDataSurvivesInList` goal (which pins the list.add arm):
// `set %bundle% = [%signed%]` binds the list through variable.set's no-type
// ShallowClone, which shares `_value` by reference — so a signed Data nested in
// the list keeps its Signature and the element still verifies. ShallowClone is
// the shared mechanism; only list.add was pinned, leaving this arm free to
// regress with no red test.
public class SignedDataSurvivesVariableSetListTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
        => _app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-setlist-" + System.Guid.NewGuid().ToString("N")[..8]));

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.System.Context;

    private async Task<data> Sign(data d)
        => await _app.RunAction<sign>(new sign { Context = Ctx, Data = d }, Ctx);

    private async Task<data> Verify(data d)
        => await _app.RunAction<verify>(new verify { Context = Ctx, Data = d }, Ctx);

    [Test] public async Task SignedDataInListLiteral_SurvivesVariableSet_AndVerifies()
    {
        var signed = await Sign(new data("signed", "hello world"));
        await Assert.That(signed.Signature).IsNotNull();

        var list = new System.Collections.Generic.List<object?> { signed };
        var action = TestAction.Create("variable", "set", ("name", "%bundle%"), ("value", list));
        var result = await action.RunAsync(Ctx);
        await result.IsSuccess();

        // %bundle[0]% — the element read returns the signed Data unchanged.
        var bound = Ctx.Variable.Get("bundle");
        var element = (bound!.Value as System.Collections.IList)![0] as data;
        await Assert.That(element).IsNotNull();
        await Assert.That(element!.Signature).IsNotNull().Because("signature survived the variable.set bind");

        var ok = await Verify(element!);
        await Assert.That(ok.GetValue<bool>()).IsTrue();
    }
}
