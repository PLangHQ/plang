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

    [Test]
    public async Task BuilderActions_Run_ReturnsTaskDataOfBuilderActionsRecord()
    {
        Assert.Fail("Pending: app.builder.Actions.@this record not yet authored");
    }

    [Test]
    public async Task BuilderGoals_Run_ReturnsTaskDataOfBuilderGoalsRecord()
    {
        Assert.Fail("Pending: app.builder.Goals.@this record not yet authored");
    }

    [Test]
    public async Task BuilderRecords_LiveAtOBPSingularFolders()
    {
        Assert.Fail("Pending: PLang/app/builder/{Types,Actions,Goals}/this.cs not yet created");
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
        Assert.Fail("Pending: builder.* typing blocked on Types/Actions/Goals records");
    }
}
