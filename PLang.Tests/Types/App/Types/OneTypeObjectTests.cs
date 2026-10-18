namespace PLang.Tests.App.Types;

// One type object per type in an app: the list's entry is what every value of it holds, a kinded type is made
// once, and the two makers of types (the root and the type list) answer their own type.
public class OneTypeObjectTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // Each concept's entry is the concept node itself once the app has started (the list's Replace).
    [Test] public async Task AConceptsEntry_IsTheConceptNode()
    {
        await Assert.That(ReferenceEquals(app.type.list[typeof(global::app.goal.@this)], app.goal)).IsTrue();
        await Assert.That(ReferenceEquals(app.type.list[typeof(global::app.test.@this)], app.test)).IsTrue();
        await Assert.That(ReferenceEquals(app.type.list["type"], app.type)).IsTrue();
    }

    // A value born after the start holds the list's current entry — never one Replace swapped out.
    [Test] public async Task AValueBornAfterStart_HoldsTheCurrentEntry()
    {
        var types = app.type.list;
        var goal = new global::app.goal.@this(types);
        var step = new global::app.goal.step.@this(types);
        var action = new global::app.goal.step.action.@this(types);
        var test = new global::app.test.@this(types) { Goal = goal };

        await Assert.That(ReferenceEquals(goal.Type, types[typeof(global::app.goal.@this)])).IsTrue();
        await Assert.That(ReferenceEquals(step.Type, types[typeof(global::app.goal.step.@this)])).IsTrue();
        await Assert.That(ReferenceEquals(action.Type, types[typeof(global::app.goal.step.action.@this)])).IsTrue();
        await Assert.That(ReferenceEquals(test.Type, types[typeof(global::app.test.@this)])).IsTrue();
        await Assert.That(ReferenceEquals(goal.Step.Type, types[typeof(global::app.goal.step.list.@this)])).IsTrue();
        await Assert.That(ReferenceEquals(step.Code.Type, types[typeof(global::app.goal.step.action.list.@this)])).IsTrue();
    }

    // The app's own lists, made at its start, hold the entry the list answers now.
    [Test] public async Task TheAppsLists_HoldTheCurrentEntry()
    {
        var types = app.type.list;
        await Assert.That(ReferenceEquals(app.goal.list.Type, types[typeof(global::app.goal.list.@this)])).IsTrue();
        await Assert.That(ReferenceEquals(app.actor.list.Type, types[typeof(global::app.actor.list.@this)])).IsTrue();
        await Assert.That(ReferenceEquals(app.actor.list.User.Context.call.Audit.Type, types[typeof(global::app.error.list.@this)])).IsTrue();
    }

    // A kinded type ({list, step}) is one object: asked twice, the same one.
    [Test] public async Task AKindedType_IsOneObject()
        => await Assert.That(ReferenceEquals(
            app.type.list[typeof(global::app.goal.step.list.@this)],
            app.type.list[typeof(global::app.goal.step.list.@this)])).IsTrue();

    // The type list answers its own type from itself: {list, type}, the one object it hands out.
    [Test] public async Task TheTypeList_IsAListOfTypes_FromItself()
    {
        var own = app.type.list.Type;
        await Assert.That(own.Name).IsEqualTo("list");
        await Assert.That(own.kind.Name).IsEqualTo("type");
        await Assert.That(ReferenceEquals(own, app.type.list[typeof(global::app.type.list.@this)])).IsTrue();
    }
}
