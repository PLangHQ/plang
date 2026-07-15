namespace PLang.Tests.App.Types;

using ItemList = global::app.type.item.list.@this<global::app.type.item.path.@this>;

// 4c.1 foundation — the identity door's container-generic rung answers {family, kind: element}
// (the choice precedent generalized), and the entity's face composes back to GetTypeName's string.
// This rung is the shared owner for property rows / action.Return / goal.variables, so the two
// must agree for every container type the catalog can see.
public class ContainerKindDoorTests
{
    [Test]
    public async Task PlangList_ResolvesToListWithElementKind()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/ckd-1");
        var entity = app.Type[typeof(ItemList)];
        await Assert.That(entity.Name).IsEqualTo("list");
        await Assert.That(entity.Kind?.Name).IsEqualTo("path");     // element rides as kind
        await Assert.That(entity.ToString()).IsEqualTo("list<path>"); // face composes back
    }

    [Test]
    public async Task ClrCollections_MapToTheSameListEntity()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/ckd-2");
        // CLR List<T>, arrays, and IList<T> all resolve to the plang list family.
        foreach (var t in new[] {
            typeof(System.Collections.Generic.List<string>),
            typeof(string[]),
            typeof(System.Collections.Generic.IReadOnlyList<long>) })
        {
            var entity = app.Type[t];
            await Assert.That(entity.Name).IsEqualTo("list");
            await Assert.That(entity.Kind).IsNotNull();
        }
    }

    [Test]
    public async Task DoorFace_MatchesGetTypeName_ForContainers()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/ckd-3");
        // The two owners agree: the entity's face == the legacy GetTypeName string.
        foreach (var t in new[] {
            typeof(ItemList),
            typeof(System.Collections.Generic.List<string>),
            typeof(string[]) })
        {
            await Assert.That(app.Type[t].ToString()).IsEqualTo(app.Type.GetTypeName(t));
        }
    }

    [Test]
    public async Task ByteArray_IsBytes_NotList()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/ckd-4");
        await Assert.That(app.Type[typeof(byte[])].Name).IsNotEqualTo("list");
    }
}
