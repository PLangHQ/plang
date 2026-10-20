using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types;

// formal — a step's actions written as calls — is a format of list: the action list declares it, and it is held as
// {list, formal}, the kind the formal writer writes.
public class FormalKindTests
{
    [Test] public async Task Formal_IsAKindOfList_AndTheFormalWritersKind()
    {
        await using var app = new global::app.@this("/test").Testing();

        var formal = app.type.list["list"].kind["formal"];

        await Assert.That(formal).IsNotNull();
        await Assert.That(formal!.Owner).IsSameReferenceAs(app.type.list["list"]);
        await Assert.That(new global::app.goal.step.action.formal.Writer(app.type.list).Kind).IsSameReferenceAs(formal);
    }
}
