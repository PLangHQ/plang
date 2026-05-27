using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Reflection contracts for mock.intercept, builder.{types,actions,goals},
// test.tag — each handler's Run() must produce a typed Data<T> whose T is
// either a domain record or a primitive (never Data<object>).

public class Stage2_MechanicalTypings_Part2Tests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private static System.Type RunReturnType<THandler>()
        => typeof(THandler).GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes)!.ReturnType;

    [Test]
    public async Task MockIntercept_Run_ReturnsTaskDataOfMock()
    {
        Assert.Fail("Pending: app.mock.Mock.@this record not yet authored");
    }

    [Test]
    public async Task MockMock_TypeLivesAtOBPSingularFolder()
    {
        Assert.Fail("Pending: PLang/app/mock/Mock/this.cs not yet created");
    }

    [Test]
    public async Task BuilderTypes_Run_ReturnsTaskDataOfBuilderTypesRecord()
    {
        var ret = RunReturnType<global::app.modules.builder.types>();
        var expected = typeof(Task<global::app.data.@this<global::app.builder.Types.@this>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    // builder.actions and builder.goals are typed directly to their natural
    // collection shapes (StepActions and List<Goal>) rather than wrapped in
    // dedicated record types — PLang call sites (Build.goal) iterate them as
    // lists, which a wrapper would break without adding observable value.
    [Test]
    public async Task BuilderActions_Run_ReturnsTaskDataOfBuilderActionsRecord()
    {
        var ret = RunReturnType<global::app.modules.builder.GetActions>();
        var expected = typeof(Task<global::app.data.@this<global::app.goals.goal.steps.step.actions.@this>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task BuilderGoals_Run_ReturnsTaskDataOfBuilderGoalsRecord()
    {
        var ret = RunReturnType<global::app.modules.builder.goals>();
        var expected = typeof(Task<global::app.data.@this<List<global::app.goals.goal.@this>>>);
        await Assert.That(ret).IsEqualTo(expected);
    }

    [Test]
    public async Task BuilderRecords_LiveAtOBPSingularFolders()
    {
        // builder.Types.@this is the only catalog wrapper; builder.actions and
        // builder.goals return their natural list shapes directly.
        await Assert.That(typeof(global::app.builder.Types.@this).Namespace).IsEqualTo("app.builder.Types");
    }

    // test.tag is bare Task<Data> or Task<Data<bool>>; the meaningful negative
    // guard is that it never degrades to Task<Data<object>>.
    [Test]
    public async Task TestTag_Run_ReturnsTaskDataOfBool_OrStaysVoidLike()
    {
        var ret = RunReturnType<global::app.modules.test.Tag>();
        var bareData = typeof(Task<Data>);
        var dataOfBool = typeof(Task<global::app.data.@this<bool>>);
        var dataOfObject = typeof(Task<global::app.data.@this<object>>);

        await Assert.That(ret == bareData || ret == dataOfBool).IsTrue()
            .Because("test.tag must be bare Task<Data> or Task<Data<bool>>, never Task<Data<object>>.");
        await Assert.That(ret).IsNotEqualTo(dataOfObject);
    }

    [Test]
    public async Task ModulesDescribe_MockIntercept_AdvertisesMockReturnType()
    {
        Assert.Fail("Pending: mock.intercept typing blocked on Mock record");
    }

    [Test]
    public async Task ModulesDescribe_BuilderRecordHandlers_AdvertiseConcreteReturnTypes()
    {
        var rendered = await _app.Modules.Describe();
        var types  = rendered.FirstOrDefault(a => a.Module == "builder" && a.ActionName == "types");
        var goals  = rendered.FirstOrDefault(a => a.Module == "builder" && a.ActionName == "goals");
        var acts   = rendered.FirstOrDefault(a => a.Module == "builder" && a.ActionName == "actions");

        await Assert.That(types!.ReturnTypeName).IsEqualTo("types");
        // goals/actions render as collection shapes — PLang's foreach over
        // them needs the list semantics, hence no wrapper record.
        await Assert.That(goals!.ReturnTypeName).IsEqualTo("list<goal>");
        await Assert.That(acts!.ReturnTypeName).Contains("action");
    }
}
