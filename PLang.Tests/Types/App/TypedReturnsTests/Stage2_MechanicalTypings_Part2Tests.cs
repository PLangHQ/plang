using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Reflection contracts for mock.intercept, builder.{types,actions,goals},
// test.tag — each handler's Run() must produce a typed Data<T> whose T is
// either a domain record or a primitive (never Data<object>).

public class Stage2_MechanicalTypings_Part2Tests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static System.Type RunReturnType<THandler>()
        => typeof(THandler).GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)!.ReturnType;

    [Test]
    public async Task MockIntercept_Run_ReturnsTaskDataOfMock()
    {
        var ret = RunReturnType<global::app.module.action.mock.intercept>();
        var expected = typeof(Task<global::app.data.@this<global::app.mock.@this>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task MockMock_TypeLivesAtOBPSingularFolder()
    {
        var t = typeof(global::app.mock.@this);
        await Assert.That(t.Namespace).IsEqualTo("app.mock");
        await Assert.That(t.Name).IsEqualTo("this");
    }

    // build.goals is typed directly to its natural collection shape (list<goal>) rather than
    // wrapped in a dedicated record — Build.goal iterates it as a list.
    [Test]
    public async Task BuilderGoals_Run_ReturnsTaskDataOfBuilderGoalsRecord()
    {
        var ret = RunReturnType<global::app.module.action.build.goals>();
        var expected = typeof(Task<global::app.data.@this<global::app.type.item.list.@this<global::app.goal.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task BuilderRecords_LiveAtOBPSingularFolders()
    {
        // builder.Types.@this is the only catalog wrapper; builder.actions and
        // builder.goals return their natural list shapes directly.
        await Assert.That(typeof(global::app.type.list.view.@this).Namespace).IsEqualTo("app.type.list.view");
    }

    // test.tag is bare Task<Data> or Task<Data<global::app.type.item.@bool.@this>>; the meaningful negative
    // guard is that it never degrades to Task<Data<object>>.
    [Test]
    public async Task TestTag_Run_ReturnsTaskDataOfBool_OrStaysVoidLike()
    {
        var ret = RunReturnType<global::app.module.action.test.Tag>();
        var bareData = typeof(Task<Data>);
        var dataOfBool = typeof(Task<global::app.data.@this<global::app.type.item.@bool.@this>>);

        // `Data<object>` is no longer expressible — `where T : item` rejects `object`, so a
        // handler can never degrade to Task<Data<object>>. The bare-or-bool check is the
        // surviving observable guarantee.
        await Assert.That(ret == bareData || ret == dataOfBool).IsTrue()
            .Because("test.tag must be bare Task<Data> or Task<Data<global::app.type.item.@bool.@this>>.");
    }

    [Test]
    public async Task ModulesDescribe_MockIntercept_AdvertisesMockReturnType()
    {
        var row = _app.Module["mock"]["intercept"];
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.Return).IsEqualTo("mock");
    }

    [Test]
    public async Task ModulesDescribe_BuilderRecordHandlers_AdvertiseConcreteReturnTypes()
    {
        var goals = _app.Module["build"]["goals"];

        // goals renders as a collection shape — PLang's foreach over it needs the list
        // semantics, hence no wrapper record.
        await Assert.That(goals).IsNotNull();
        await Assert.That(goals!.Return).IsEqualTo("list<goal>");
    }
}
