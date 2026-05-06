namespace PLang.Tests.App.ChannelsTests;

// Stage 7 — Flat App.Services collection + Service type + Actor cleanup.
// Architect: stage-7-services.md.

public class Stage7_AppServicesTests
{
    [Test]
    public async Task Services_NewWithParent_CreatesService_AddsToCollection()
    {
        // app.Services.New(parent: app.User) returns a Service whose Parent is
        // app.User, and the service is reachable via app.Services enumeration.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Service_Channels_IsEmptyOnConstruction()
    {
        // service.Channels has zero registered channels initially. Caller
        // populates them per outbound call.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Service_Identity_NavigatesToAppSystemIdentity()
    {
        // service.Identity is reference-equal (or value-equal — coder choice)
        // to app.System.Identity. Plan: "Service signs with System identity, always."
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Service_Parent_IsTheActorPassedAtCreation()
    {
        // service.Parent set to whoever was passed via app.Services.New(parent: ...).
        // Used for audit/tracing, not for signing.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Service_AwaitUsing_RemovesFromCollection_AndDisposesChannels()
    {
        //   await using var s = app.Services.New(parent: app.User);
        //   s.Channels.Register(...)
        // After the using block: s no longer in app.Services AND its Channels
        // were each disposed (Channel.IsOpen == false / underlying Stream closed
        // when ownsStream=true).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task TwoParallelServices_DontCollide_OnChannelNames()
    {
        // service A and service B both register a channel called "input".
        // Each service sees only its own. No shared registry, no name collision.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ActorValidValues_DropsToUserAndSystem()
    {
        // Actor.@this.ValidValues returns ["user", "system"] only. "service"
        // is no longer an Actor — it's a separate type under App.Services.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Actor_NoLongerHasEscalationLevel()
    {
        // Reflection: Actor.@this has no `EscalationLevel` property.
        // Architect plan: "EscalationLevel — dead code, remove."
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
