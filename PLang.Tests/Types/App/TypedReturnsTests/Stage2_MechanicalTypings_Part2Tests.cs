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
        var ret = RunReturnType<global::app.module.mock.intercept>();
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

    [Test]
    public async Task BuilderTypes_Run_ReturnsTaskDataOfBuilderTypesRecord()
    {
        var ret = RunReturnType<global::app.module.builder.types>();
        var expected = typeof(Task<global::app.data.@this<global::app.builder.type.@this>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    // builder.actions and builder.goals are typed directly to their natural
    // collection shapes (StepActions and List<Goal>) rather than wrapped in
    // dedicated record types — PLang call sites (Build.goal) iterate them as
    // lists, which a wrapper would break without adding observable value.
    [Test]
    public async Task BuilderActions_Run_ReturnsTaskDataOfBuilderActionsRecord()
    {
        var ret = RunReturnType<global::app.module.builder.GetActions>();
        var expected = typeof(Task<global::app.data.@this<global::app.goal.steps.step.actions.@this>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task BuilderGoals_Run_ReturnsTaskDataOfBuilderGoalsRecord()
    {
        var ret = RunReturnType<global::app.module.builder.goals>();
        var expected = typeof(Task<global::app.data.@this<global::app.type.list.@this<global::app.goal.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task BuilderRecords_LiveAtOBPSingularFolders()
    {
        // builder.Types.@this is the only catalog wrapper; builder.actions and
        // builder.goals return their natural list shapes directly.
        await Assert.That(typeof(global::app.builder.type.@this).Namespace).IsEqualTo("app.builder.type");
    }

    // test.tag is bare Task<Data> or Task<Data<global::app.type.@bool.@this>>; the meaningful negative
    // guard is that it never degrades to Task<Data<object>>.
    [Test]
    public async Task TestTag_Run_ReturnsTaskDataOfBool_OrStaysVoidLike()
    {
        var ret = RunReturnType<global::app.module.test.Tag>();
        var bareData = typeof(Task<Data>);
        var dataOfBool = typeof(Task<global::app.data.@this<global::app.type.@bool.@this>>);

        // `Data<object>` is no longer expressible — `where T : item` rejects `object`, so a
        // handler can never degrade to Task<Data<object>>. The bare-or-bool check is the
        // surviving observable guarantee.
        await Assert.That(ret == bareData || ret == dataOfBool).IsTrue()
            .Because("test.tag must be bare Task<Data> or Task<Data<global::app.type.@bool.@this>>.");
    }

    [Test]
    public async Task ModulesDescribe_MockIntercept_AdvertisesMockReturnType()
    {
        var rendered = await _app.Module.Describe();
        var row = rendered.FirstOrDefault(a => a.Module == "mock" && a.ActionName == "intercept");
        await Assert.That(row).IsNotNull();
        await Assert.That(row!.ReturnTypeName).IsEqualTo("mock");
    }

    [Test]
    public async Task ModulesDescribe_BuilderRecordHandlers_AdvertiseConcreteReturnTypes()
    {
        var rendered = await _app.Module.Describe();
        var types  = rendered.FirstOrDefault(a => a.Module == "builder" && a.ActionName == "types");
        var goals  = rendered.FirstOrDefault(a => a.Module == "builder" && a.ActionName == "goals");
        var acts   = rendered.FirstOrDefault(a => a.Module == "builder" && a.ActionName == "actions");

        await Assert.That(types!.ReturnTypeName).IsEqualTo("type");
        // goals/actions render as collection shapes — PLang's foreach over
        // them needs the list semantics, hence no wrapper record.
        await Assert.That(goals!.ReturnTypeName).IsEqualTo("list<goal>");
        await Assert.That(acts!.ReturnTypeName).Contains("action");
    }
}
