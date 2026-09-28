using app.error;
using app.module.action.mock;
using Mock = app.@event.binding.mock.@this;

namespace PLang.Tests.App.Modules.mock;

// A mock is a binding before an action starts, on the module or catalog action it names; it is its own record.
public class MockTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/app");

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<Mock> Intercept(string pattern, object? returns = null)
    {
        var action = new intercept(Ctx) { Pattern = (global::app.type.item.text.@this)pattern };
        if (returns != null) action = new intercept(Ctx)
        {
            Pattern = (global::app.type.item.text.@this)pattern,
            Return = new global::app.data.@this("", returns, context: Ctx),
        };
        var result = await action.Start();
        await result.IsSuccess();
        return (await result.Value())!;
    }

    // --- mock.intercept ---

    [Test]
    public async Task Intercept_WithAReturn_IsAMock_NotASpy()
    {
        var mock = await Intercept("file.read", "test content");

        await Assert.That(mock.Pattern).IsEqualTo("file.read");
        await Assert.That(mock.CallCount).IsEqualTo(0);
        await Assert.That(mock.IsSpy).IsFalse();
    }

    [Test]
    public async Task Intercept_WithNothing_IsASpy()
        => await Assert.That((await Intercept("output.write")).IsSpy).IsTrue();

    [Test]
    public async Task Intercept_AnAction_BindsBeforeTheCatalogActionStarts()
    {
        var mock = await Intercept("file.read", "mocked");

        var before = _app.Module("file")["read"]!.on.start.before;
        await Assert.That(before.Count).IsEqualTo(1);
        await Assert.That(before[0]).IsSameReferenceAs(mock);
    }

    [Test]
    public async Task Intercept_AModule_BindsBeforeTheModuleStarts()
    {
        var mock = await Intercept("file", "mocked");

        await Assert.That(_app.Module("file").on.start.before[0]).IsSameReferenceAs(mock);
    }

    [Test]
    public async Task Intercept_WhatIsNotThere_IsTheError()
    {
        var result = await new intercept(Ctx) { Pattern = (global::app.type.item.text.@this)"file.nosuch" }.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    // --- mock.verify ---

    [Test]
    public async Task Verify_CorrectCount_Passes()
    {
        var mock = await Intercept("file.read", "x");
        mock.Calls.Add(new global::app.@event.binding.mock.Call());
        mock.Calls.Add(new global::app.@event.binding.mock.Call());

        var result = await new Verify(Ctx) { Mock = mock, ExpectedCount = (global::app.type.item.number.@this)2 }.Start();
        await result.IsSuccess();
    }

    [Test]
    public async Task Verify_WrongCount_Fails()
    {
        var mock = await Intercept("file.read", "x");
        mock.Calls.Add(new global::app.@event.binding.mock.Call());

        var result = await new Verify(Ctx) { Mock = mock, ExpectedCount = (global::app.type.item.number.@this)3 }.Start();
        await result.IsFailure();
        await Assert.That(result.Error is AssertionError).IsTrue();
    }

    [Test]
    public async Task Verify_CustomMessage_IncludedInError()
    {
        var mock = await Intercept("file.read", "x");

        var result = await new Verify(Ctx)
        {
            Mock = mock,
            ExpectedCount = (global::app.type.item.number.@this)1,
            Message = (global::app.type.item.text.@this)"file.read should be called once",
        }.Start();
        await result.IsFailure();
        await Assert.That((result.Error as AssertionError)!.UserMessage).IsEqualTo("file.read should be called once");
    }

    // --- mock.reset ---

    [Test]
    public async Task Reset_TakesTheMockOff_AndClearsItsCalls()
    {
        var mock = await Intercept("file.read", "mocked");
        mock.Calls.Add(new global::app.@event.binding.mock.Call());

        var result = await new Reset(Ctx) { Mock = mock }.Start();

        await result.IsSuccess();
        await Assert.That(_app.Module("file")["read"]!.on.start.before.Count).IsEqualTo(0);
        await Assert.That(mock.CallCount).IsEqualTo(0);
    }

    // --- a parameter's expected value: the same text, case aside ---

    [Test]
    [Arguments("config.json", "config.json", true)]
    [Arguments("config.json", "data.json", false)]
    [Arguments("Config.JSON", "config.json", true)]
    [Arguments("a.b", "axb", false)]
    public async Task Match(string expected, string actual, bool matches)
        => await Assert.That(Mock.Match(expected, actual)).IsEqualTo(matches);

    [Test]
    public async Task Match_Nulls()
    {
        await Assert.That(Mock.Match(null, null)).IsTrue();
        await Assert.That(Mock.Match(null, "value")).IsFalse();
    }
}
