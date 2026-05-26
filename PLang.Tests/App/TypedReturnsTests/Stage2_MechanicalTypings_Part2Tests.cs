namespace PLang.Tests.App.TypedReturnsTests;

// Stage 2 — Mechanical typings, part 2: mock.intercept, builder.{types,actions,goals}, test.tag.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 2)
// Plan: .bot/typed-action-returns/architect/plan.md (A.1)

public class Stage2_MechanicalTypings_Part2Tests
{
    [Test]
    public async Task MockIntercept_Run_ReturnsTaskDataOfMock()
        // The mock.intercept handler returns Task<Data<app.mock.Mock.@this>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task MockMock_TypeLivesAtOBPSingularFolder()
        // typeof(app.mock.Mock.@this) exists at PLang/app/mock/Mock/this.cs.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderTypes_Run_ReturnsTaskDataOfBuilderTypesRecord()
        // app.modules.builder.TypesHandler.Run returns Task<Data<app.builder.Types.@this>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderActions_Run_ReturnsTaskDataOfBuilderActionsRecord()
        // app.modules.builder.ActionsHandler.Run returns Task<Data<app.builder.Actions.@this>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderGoals_Run_ReturnsTaskDataOfBuilderGoalsRecord()
        // app.modules.builder.GoalsHandler.Run returns Task<Data<app.builder.Goals.@this>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuilderRecords_LiveAtOBPSingularFolders()
        // app.builder.Types.@this, app.builder.Actions.@this, app.builder.Goals.@this all exist under their own folders.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task TestTag_Run_ReturnsTaskDataOfBool_OrStaysVoidLike()
        // Per architect: bool is low-priority; either Task<Data<bool>> or bare Task<Data> is acceptable, but NOT Task<Data<object>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task ModulesDescribe_MockIntercept_AdvertisesMockReturnType()
        // Catalog renders `→ returns mock` for mock.intercept.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task ModulesDescribe_BuilderRecordHandlers_AdvertiseConcreteReturnTypes()
        // Catalog renders `→ returns types`/`actions`/`goals` for the three builder.* handlers.
        => Assert.Fail("Not implemented");
}
