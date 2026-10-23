namespace PLang.Tests.App.Goals;

/// <summary>
/// Stage 1 (item-doors, decision 867 + 880): a type named in a step is the REGISTERED
/// one, with its ClrType known — asked through <c>app.type.list</c>, the one door for
/// every type. The three places that today ask "is this an action slot?" by comparing
/// <c>slot.Type.Name == "action"</c> (<c>goal/step/action/this.Validate.cs:54–55</c>,
/// <c>formal/reader.cs:221</c>, <c>:349</c>, <c>serializer/Reader.cs:153–154</c>; F8.1)
/// can only do that because a slot typed <c>{list, kind: action}</c> doesn't carry the
/// registered action's identity at its kind — the Name string is all they have.
///
/// <para>Asked through <c>app.type.list["action"]</c>, the registered action type carries
/// its ClrType (passes today — the sanity check). Asked through the name-and-kind door
/// (<c>app.type.list[new type("list","action"), ctx]</c>), today's result's
/// <c>.kind.ClrType</c> is <b>null</b> — <c>element.@this</c> wraps the resolved action
/// type but doesn't carry its ClrType up through the kind's own type face. After stage 1
/// the kind IS the registered type (born whole, same identity), and <c>.kind.ClrType</c>
/// answers <c>typeof(action.@this)</c>.</para>
/// </summary>
public class ActionSlotIsByTypeTests
{
    private string _dir = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_actionslot_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_dir);
        _app = new global::app.@this(_dir);
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); } catch { }
        try { System.IO.Directory.Delete(_dir, true); } catch (System.IO.IOException) { }
    }

    [Test] public async Task TheRegisteredActionType_CarriesItsClrType_AskedThroughTheList()
    {
        var registered = _app.type.list["action"];
        await Assert.That(registered.ClrType).IsEqualTo(typeof(global::app.goal.step.action.@this));
    }

    [Test] public async Task AKindedSlot_ListOfAction_CarriesTheRegisteredActionAtItsKind()
    {
        var ctx = _app.actor.list.System.Context;
        var listOfAction = _app.type.list[new global::app.type.@this("list", "action"), ctx];

        // The slot's name is "list"; its kind names "action".
        await Assert.That(listOfAction.Name).IsEqualTo("list");
        await Assert.That(listOfAction.kind.Name).IsEqualTo("action");

        // The slot's kind IS the registered action type — carries its ClrType. Red today:
        // Full() builds the kind via element.@this(action_type), which wraps the registered
        // action but doesn't surface its ClrType on the kind's own type face (kind.ClrType
        // is null until stage 1 births the kind holding the registered type whole).
        await Assert.That(listOfAction.kind.ClrType).IsEqualTo(typeof(global::app.goal.step.action.@this));
    }
}
