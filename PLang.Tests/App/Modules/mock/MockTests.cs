using global::App.Actor.Context;
using App;
using global::App.Errors;
using global::App.Variables;
using global::App.modules.mock;
using global::App.modules.mock.types;

namespace PLang.Tests.App.Modules.mock;

public class MockTests
{
    private (global::App.Actor.Context.@this context, Variables memory, global::App.@this engine) CreateContext()
    {
        var engine = new global::App.@this("/app");
        var memory = new Variables();
        var context = new global::App.Actor.Context.@this(engine, memory);
        return (context, memory, engine);
    }

    // --- mock.action: simple return ---

    [Test]
    public async Task Action_SimpleReturn_CreatesHandle()
    {
        var (context, _, _) = CreateContext();
        var action = new MockAction
        {
            Context = context,
            ActionPattern = "file.read",
            ReturnValue = "test content"
        };

        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNotNull();
        await Assert.That(result.Value is MockHandle).IsTrue();

        var handle = (MockHandle)result.Value!;
        await Assert.That(handle.ActionPattern).IsEqualTo("file.read");
        await Assert.That(handle.CallCount).IsEqualTo(0);
        await Assert.That(handle.IsSpy).IsFalse();
    }

    [Test]
    public async Task Action_Spy_CreatesSpyHandle()
    {
        var (context, _, _) = CreateContext();
        var action = new MockAction
        {
            Context = context,
            ActionPattern = "output.write"
        };

        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();

        var handle = (MockHandle)result.Value!;
        await Assert.That(handle.IsSpy).IsTrue();
    }

    [Test]
    public async Task Action_RegistersBeforeActionEvent()
    {
        var (context, _, _) = CreateContext();
        var action = new MockAction
        {
            Context = context,
            ActionPattern = "file.read",
            ReturnValue = "mocked"
        };

        var beforeCount = context.Events.Count;
        await action.Run();
        var afterCount = context.Events.Count;

        await Assert.That(afterCount).IsEqualTo(beforeCount + 1);
    }

    [Test]
    public async Task Action_EventBindingId_IsPopulated()
    {
        var (context, _, _) = CreateContext();
        var action = new MockAction
        {
            Context = context,
            ActionPattern = "file.read",
            ReturnValue = "mocked"
        };

        var result = await action.Run();
        var handle = (MockHandle)result.Value!;

        await Assert.That(handle.EventBindingId).IsNotNull();
        await Assert.That(handle.EventBindingId.Length).IsGreaterThan(0);
    }

    // --- mock.verify ---

    [Test]
    public async Task Verify_CorrectCount_Passes()
    {
        var (context, _, _) = CreateContext();
        var handle = new MockHandle
        {
            Id = "test",
            ActionPattern = "file.read"
        };
        handle.RecordCall(new Dictionary<string, object?> { ["path"] = "test.txt" });
        handle.RecordCall(new Dictionary<string, object?> { ["path"] = "other.txt" });

        var verify = new Verify
        {
            Context = context,
            Mock = handle,
            ExpectedCount = 2
        };

        var result = await verify.Run();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Verify_WrongCount_Fails()
    {
        var (context, _, _) = CreateContext();
        var handle = new MockHandle
        {
            Id = "test",
            ActionPattern = "file.read"
        };
        handle.RecordCall(new Dictionary<string, object?> { ["path"] = "test.txt" });

        var verify = new Verify
        {
            Context = context,
            Mock = handle,
            ExpectedCount = 3
        };

        var result = await verify.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error is AssertionError).IsTrue();
    }

    [Test]
    public async Task Verify_CustomMessage_IncludedInError()
    {
        var (context, _, _) = CreateContext();
        var handle = new MockHandle
        {
            Id = "test",
            ActionPattern = "file.read"
        };

        var verify = new Verify
        {
            Context = context,
            Mock = handle,
            ExpectedCount = 1,
            Message = "file.read should be called once"
        };

        var result = await verify.Run();
        await Assert.That(result.Success).IsFalse();
        var error = result.Error as AssertionError;
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.UserMessage).IsEqualTo("file.read should be called once");
    }

    // --- mock.reset ---

    [Test]
    public async Task Reset_SpecificMock_RemovesBinding()
    {
        var (context, _, _) = CreateContext();

        // Register a mock
        var mockAction = new MockAction
        {
            Context = context,
            ActionPattern = "file.read",
            ReturnValue = "mocked"
        };
        var mockResult = await mockAction.Run();
        var handle = (MockHandle)mockResult.Value!;

        var countBefore = context.Events.Count;

        // Reset the specific mock
        var reset = new Reset
        {
            Context = context,
            Mock = handle
        };
        var resetResult = await reset.Run();
        await Assert.That(resetResult.Success).IsTrue();
        await Assert.That(context.Events.Count).IsEqualTo(countBefore - 1);
    }

    [Test]
    public async Task Reset_AllMocks_RemovesAllMockBindings()
    {
        var (context, _, _) = CreateContext();

        // Register two mocks
        var mock1 = new MockAction
        {
            Context = context,
            ActionPattern = "file.read",
            ReturnValue = "mocked1"
        };
        await mock1.Run();

        var mock2 = new MockAction
        {
            Context = context,
            ActionPattern = "output.write",
            ReturnValue = "mocked2"
        };
        await mock2.Run();

        await Assert.That(context.Events.Count).IsGreaterThanOrEqualTo(2);

        // Reset all mocks
        var reset = new Reset
        {
            Context = context,
            Mock = null
        };
        var resetResult = await reset.Run();
        await Assert.That(resetResult.Success).IsTrue();
        await Assert.That(context.Events.Count).IsEqualTo(0);
    }

    // --- MockHandle tracking ---

    [Test]
    public async Task MockHandle_RecordCall_TracksParameters()
    {
        var handle = new MockHandle
        {
            Id = "test",
            ActionPattern = "file.read"
        };

        handle.RecordCall(new Dictionary<string, object?> { ["path"] = "config.json" });
        handle.RecordCall(new Dictionary<string, object?> { ["path"] = "data.json" });

        await Assert.That(handle.CallCount).IsEqualTo(2);
        await Assert.That(handle.Calls[0].Parameters["path"]).IsEqualTo("config.json");
        await Assert.That(handle.Calls[1].Parameters["path"]).IsEqualTo("data.json");
    }

    // --- ToRegex ---

    [Test]
    public async Task ToRegex_PlainString_ExactMatch()
    {
        var regex = MockAction.ToRegex("config.json");
        await Assert.That(regex).IsEqualTo(@"^config\.json$");
    }

    [Test]
    public async Task ToRegex_WildcardStar_BecomesRegexDotStar()
    {
        var regex = MockAction.ToRegex("https://example.org/api/*");
        await Assert.That(regex).IsEqualTo(@"^https://example\.org/api/.*$");
    }

    [Test]
    public async Task ToRegex_LeadingStar_BecomesRegexDotStar()
    {
        var regex = MockAction.ToRegex("*.example.org");
        await Assert.That(regex).IsEqualTo(@"^.*\.example\.org$");
    }

    [Test]
    public async Task ToRegex_MultipleStar_AllConverted()
    {
        var regex = MockAction.ToRegex("*keyword*");
        await Assert.That(regex).IsEqualTo(@"^.*keyword.*$");
    }

    [Test]
    public async Task ToRegex_RegexPattern_UsedAsIs()
    {
        var regex = MockAction.ToRegex(@"\d+\.json");
        await Assert.That(regex).IsEqualTo(@"^\d+\.json$");
    }

    // --- MatchValue ---

    [Test]
    public async Task MatchValue_ExactString_Matches()
    {
        await Assert.That(MockAction.MatchValue("config.json", "config.json")).IsTrue();
    }

    [Test]
    public async Task MatchValue_ExactString_NoMatch()
    {
        await Assert.That(MockAction.MatchValue("config.json", "data.json")).IsFalse();
    }

    [Test]
    public async Task MatchValue_Wildcard_Matches()
    {
        await Assert.That(MockAction.MatchValue("https://example.org/api/*", "https://example.org/api/users")).IsTrue();
    }

    [Test]
    public async Task MatchValue_Wildcard_NoMatch()
    {
        await Assert.That(MockAction.MatchValue("https://example.org/api/*", "https://other.org/api/users")).IsFalse();
    }

    [Test]
    public async Task MatchValue_CaseInsensitive()
    {
        await Assert.That(MockAction.MatchValue("Config.JSON", "config.json")).IsTrue();
    }

    [Test]
    public async Task MatchValue_NullBoth_Matches()
    {
        await Assert.That(MockAction.MatchValue(null, null)).IsTrue();
    }

    [Test]
    public async Task MatchValue_NullPattern_NoMatch()
    {
        await Assert.That(MockAction.MatchValue(null, "value")).IsFalse();
    }

    [Test]
    public async Task MatchValue_ContainsWildcard_Matches()
    {
        await Assert.That(MockAction.MatchValue("*keyword*", "this has keyword inside")).IsTrue();
    }
}
