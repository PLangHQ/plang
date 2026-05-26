namespace PLang.Tests.App.TypedReturnsTests;

// Stage 2 — Mechanical typings, part 1: test.*, goal.getTypes, output.ask, channel.set.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 2)
// Plan: .bot/typed-action-returns/architect/plan.md (A.1)

public class Stage2_MechanicalTypings_Part1Tests
{
    [Test]
    public async Task TestDiscover_Run_ReturnsTaskDataListOfTest()
        // Reflection: app.modules.test.DiscoverHandler.Run returns Task<Data<List<app.tester.Test.@this>>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task TestRun_Run_ReturnsTaskDataOfResults()
        // app.modules.test.RunHandler.Run returns Task<Data<app.tester.Results>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task GoalGetTypes_Run_ReturnsTaskDataOfStronglyTypedRecord()
        // No more Dictionary<string,string>. Reflection: GetTypesHandler.Run returns Task<Data<List<TypeInfo>>> (or coder-chosen concrete shape).
        => Assert.Fail("Not implemented");

    [Test]
    public async Task OutputAsk_Run_ReturnsTaskDataOfString()
        // app.modules.output.AskHandler.Run returns Task<Data<string>>.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task ChannelSet_Run_ReturnsBareTaskOfData_VoidLike()
        // app.modules.channel.SetHandler.Run returns Task<Data> (no <T>) — channel.set produces no value.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task ModulesDescribe_TestDiscover_AdvertisesListOfTestReturnType()
        // Catalog rendered via Modules.Describe() shows `→ returns list<test>` for test.discover.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task ModulesDescribe_TestRun_AdvertisesResultsReturnType()
        // Catalog renders `→ returns results` for test.run.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task ModulesDescribe_OutputAsk_AdvertisesStringReturnType()
        // Catalog renders `→ returns string` for output.ask.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task ModulesDescribe_ChannelSet_OmitsReturnsLine()
        // Catalog has no `→ returns` line for channel.set — bare Task<Data> means no value emitted.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task DataValueFromTypedRun_NotDoubleWrapped()
        // Calling each handler's Run() and reading .Value yields the raw T, not nested Data<Data<T>> (footgun guard).
        => Assert.Fail("Not implemented");
}
