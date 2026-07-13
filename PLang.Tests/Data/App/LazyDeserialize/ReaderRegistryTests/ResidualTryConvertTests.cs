using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// The central conversion door is gone — a value lowers ITSELF to a CLR target via its
// own `Clr`, a list walks its elements through theirs. These rows pin that self-lowering
// (scalar → numeric target, sequence → typed list) after the door was removed.
public class ResidualTryConvertTests
{
    private static readonly global::app.actor.context.@this Ctx = global::PLang.Tests.TestApp.SharedContext;

    [Test] public async Task Scalar_LowersToNumericTarget()
    {
        // A text scalar lowers itself to a numeric CLR target (invariant ChangeType).
        await Assert.That(new global::app.type.item.text.@this("5").Clr<long>()).IsEqualTo(5L);
    }

    [Test] public async Task Number_LowersToAssignableTarget()
    {
        await Assert.That(((global::app.type.item.number.@this)5).Clr<long>()).IsEqualTo(5L);
    }

    [Test] public async Task List_LowersEachElement_ToTypedList()
    {
        var lst = new global::app.type.item.list.@this(Ctx);
        lst.Add(new global::app.type.item.text.@this("1"));
        lst.Add(new global::app.type.item.text.@this("2"));
        lst.Add(new global::app.type.item.text.@this("3"));

        var list = lst.Clr<System.Collections.Generic.List<int>>();
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That(list[0]).IsEqualTo(1);
        await Assert.That(list[2]).IsEqualTo(3);
    }
}
