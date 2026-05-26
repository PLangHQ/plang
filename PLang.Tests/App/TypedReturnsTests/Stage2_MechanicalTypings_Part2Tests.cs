using System.Reflection;

namespace PLang.Tests.App.TypedReturnsTests;

// Stage 2 — Mechanical typings, part 2: mock.intercept, builder.{types,actions,goals}, test.tag.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 2)
// Plan: .bot/typed-action-returns/architect/plan.md (A.1)
//
// Status snapshot at this commit:
//   ✅ test.tag         → Task<Data> bare (architect allows: "bool OR void-like, NOT Task<Data<object>>")
//   ⏳ mock.intercept   → still Task<Data>; new Mock record at app/mock/Mock/this.cs not yet authored
//   ⏳ builder.types    → still Task<Data>; new Types record at app/builder/Types/this.cs not yet authored
//   ⏳ builder.actions  → still Task<Data>; new Actions record at app/builder/Actions/this.cs not yet authored
//   ⏳ builder.goals    → still Task<Data>; new Goals record at app/builder/Goals/this.cs not yet authored

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
        Assert.Fail("Pending: app.mock.Mock.@this record not yet authored; see file header.");
    }

    [Test]
    public async Task MockMock_TypeLivesAtOBPSingularFolder()
    {
        Assert.Fail("Pending: PLang/app/mock/Mock/this.cs not yet created; see file header.");
    }

    [Test]
    public async Task BuilderTypes_Run_ReturnsTaskDataOfBuilderTypesRecord()
    {
        Assert.Fail("Pending: app.builder.Types.@this record not yet authored; see file header.");
    }

    [Test]
    public async Task BuilderActions_Run_ReturnsTaskDataOfBuilderActionsRecord()
    {
        Assert.Fail("Pending: app.builder.Actions.@this record not yet authored; see file header.");
    }

    [Test]
    public async Task BuilderGoals_Run_ReturnsTaskDataOfBuilderGoalsRecord()
    {
        Assert.Fail("Pending: app.builder.Goals.@this record not yet authored; see file header.");
    }

    [Test]
    public async Task BuilderRecords_LiveAtOBPSingularFolders()
    {
        Assert.Fail("Pending: PLang/app/builder/{Types,Actions,Goals}/this.cs not yet created; see file header.");
    }

    // test.tag's Run currently returns bare Task<Data> — accepted by the architect's
    // "bool OR void-like" allowance for low-priority typing. The strict negative
    // assertion (no Task<Data<object>>) is the meaningful guard here.
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
        Assert.Fail("Pending: mock.intercept typing blocked on Mock record; see file header.");
    }

    [Test]
    public async Task ModulesDescribe_BuilderRecordHandlers_AdvertiseConcreteReturnTypes()
    {
        Assert.Fail("Pending: builder.* typing blocked on Types/Actions/Goals records; see file header.");
    }
}
