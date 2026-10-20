namespace PLang.Tests.App.Types;

// One type object per type in an app: the list's entry is what every value of it holds, a kinded type is made
// once, and the two makers of types (the root and the type list) answer their own type.
public class OneTypeObjectTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // Each concept's entry is its plain type, the one its node holds; the node is no type.
    [Test] public async Task AConceptsEntry_IsThePlainType_ItsNodeHolds()
    {
        await Assert.That(ReferenceEquals(app.type.list[typeof(global::app.goal.@this)], app.goal.type)).IsTrue();
        await Assert.That(ReferenceEquals(app.type.list[typeof(global::app.test.@this)], app.test.type)).IsTrue();
        await Assert.That(ReferenceEquals(app.type.list["type"], app.type.type)).IsTrue();
        await Assert.That(app.type.list["goal"].GetType()).IsEqualTo(typeof(global::app.type.@this));
    }

    // A value born after the start holds the list's entry for its class.
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
    // a null read off the wire for a declared type holds that type's one object, the same as a value of it
    [Test] public async Task ATypedNullReadOffTheWire_HoldsTheAppsType()
    {
        var context = app.actor.list.User.Context;
        var bytes = System.Text.Encoding.UTF8.GetBytes("null");
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8, bytes);
        var read = app.type.list.Reader.Typed("guid", null)!.Read(ref reader, null, new global::app.type.reader.ReadContext(context));
        await Assert.That(read.IsNull).IsTrue();
        await Assert.That(ReferenceEquals(read.Type, app.type.list["guid"])).IsTrue();
    }

    // a kindless text declared text<md> is born again holding the app's {text, md}, made by that type's own door
    [Test] public async Task AKindlessTextDeclaredAKind_HoldsTheAppsKindedType()
    {
        var context = app.actor.list.User.Context;
        var md = app.type.list[new global::app.type.@this("text", "md"), context];
        var made = md.Make(app.type.list["text"].Make("a", context), context);
        await Assert.That(ReferenceEquals(made.Type, md)).IsTrue();
        await Assert.That(made.ToString()).IsEqualTo("a");
    }

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
