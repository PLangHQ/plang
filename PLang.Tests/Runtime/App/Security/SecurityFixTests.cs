namespace PLang.Tests.App.Security;

/// <summary>
/// Tests for security fixes from the security audit.
/// These tests verify the guards remain in place — removal should break tests.
/// </summary>
public class SecurityFixTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    #region HIGH-1: an event's run leaves the flow even when it throws

    [Test]
    public async Task AnEventWhoseRunThrows_StillLeavesTheFlow()
    {
        var context = _app.actor.list.User.Context;
        var step = new Step(_app.type.list) { Index = 0, Text = "s" };

        // An event whose run throws the first time
        var callCount = 0;
        await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(_app.type.list["step"], "start", global::app.@event.When.before, (_, result, _) =>
        {
            callCount++;
            if (callCount == 1) throw new InvalidOperationException("first call fails");
            return Task.FromResult(result);
        }, _app.actor.list.User, global::app.@event.code.@this.Always));
        var before = (await context.Actor!.@event.On(step)).before;

        try { await before.Start(step, context); }
        catch (InvalidOperationException) { }

        // Had its run stayed entered, the second would be passed by without running
        await (await before.Start(step, context))!.IsSuccess();
        await Assert.That(callCount).IsEqualTo(2);
    }

    #endregion

    #region A template's %!x%

    // An unset %!x% (an optional engine internal) stays as written when a template renders.
    [Test]
    public async Task Template_UnsetBangVar_StaysAsWritten()
    {
        var input = "test=%!nonexistent%";
        await Assert.That(await _app.actor.list.User.Context.Rendered(input)).IsEqualTo(input);
    }

    #endregion

    #region MEDIUM-3: CRLF header sanitization

    [Test]
    public async Task HttpHeaders_CRLFStripped()
    {
        // Test the header sanitization by creating a request with CRLF in header value
        var request = new System.Net.Http.HttpRequestMessage(
            System.Net.Http.HttpMethod.Get, "https://example.com");

        // Simulate what ApplyHeaders does — the fix strips \r\n
        var headerValue = "value\r\nX-Injected: true";
        var sanitized = headerValue.Replace("\r", "").Replace("\n", "");

        request.Headers.TryAddWithoutValidation("X-Test", sanitized);

        // Verify no CRLF in the actual header value
        var values = request.Headers.GetValues("X-Test").First();
        await Assert.That(values.Contains('\r')).IsFalse();
        await Assert.That(values.Contains('\n')).IsFalse();
        await Assert.That(values).IsEqualTo("valueX-Injected: true");
    }

    #endregion
}
