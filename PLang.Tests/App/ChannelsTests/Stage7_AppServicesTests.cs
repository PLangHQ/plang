using System.Reflection;
using AppService = global::App.Services.Service.@this;

namespace PLang.Tests.App.ChannelsTests;

// Stage 7 — Flat App.Services collection + Service type + Actor cleanup.

public class Stage7_AppServicesTests
{
    [Test]
    public async Task Services_NewWithParent_CreatesService_AddsToCollection()
    {
        await using var app = new global::App.@this("/tmp/s7a");
        var s = app.Services.New(parent: app.User);
        await Assert.That(s.Parent).IsEqualTo(app.User);
        await Assert.That(app.Services.Count).IsEqualTo(1);
        await Assert.That(app.Services.Contains(s)).IsTrue();
    }

    [Test]
    public async Task Service_Channels_IsEmptyOnConstruction()
    {
        await using var app = new global::App.@this("/tmp/s7b");
        var s = app.Services.New(parent: app.User);
        await Assert.That(s.Channels.ChannelNames.Any()).IsFalse();
    }

    [Test]
    public async Task Service_Identity_NavigatesToAppSystemIdentity()
    {
        await using var app = new global::App.@this("/tmp/s7c");
        app.System.Identity = new global::App.modules.identity.Identity { Name = "system-id" };
        var s = app.Services.New(parent: app.User);
        await Assert.That(s.Identity).IsEqualTo(app.System.Identity);
    }

    [Test]
    public async Task Service_Parent_IsTheActorPassedAtCreation()
    {
        await using var app = new global::App.@this("/tmp/s7d");
        var sUser = app.Services.New(parent: app.User);
        var sSystem = app.Services.New(parent: app.System);
        await Assert.That(sUser.Parent).IsEqualTo(app.User);
        await Assert.That(sSystem.Parent).IsEqualTo(app.System);
    }

    [Test]
    public async Task Service_AwaitUsing_RemovesFromCollection_AndDisposesChannels()
    {
        await using var app = new global::App.@this("/tmp/s7e");
        AppService captured;
        global::App.Channels.Channel.@this disposedCh;
        {
            await using var s = app.Services.New(parent: app.User);
            captured = s;
            disposedCh = StreamChannel.Memory("input");
            s.Channels.Register(disposedCh);
            await Assert.That(app.Services.Count).IsEqualTo(1);
        }
        await Assert.That(app.Services.Contains(captured)).IsFalse();
        await Assert.That(disposedCh.IsOpen).IsFalse();
    }

    [Test]
    public async Task TwoParallelServices_DontCollide_OnChannelNames()
    {
        await using var app = new global::App.@this("/tmp/s7f");
        await using var sA = app.Services.New(parent: app.User);
        await using var sB = app.Services.New(parent: app.User);
        sA.Channels.Register(StreamChannel.Memory("input"));
        sB.Channels.Register(StreamChannel.Memory("input"));
        await Assert.That(sA.Channels.Get("input")).IsNotEqualTo(sB.Channels.Get("input"));
    }

    [Test]
    public async Task ActorValidValues_DropsToUserAndSystem()
    {
        var values = global::App.Actor.@this.ValidValues;
        await Assert.That(values).Contains("user");
        await Assert.That(values).Contains("system");
        await Assert.That(values).DoesNotContain("service");
    }

    [Test]
    public async Task Actor_NoLongerHasEscalationLevel()
    {
        var prop = typeof(global::App.Actor.@this).GetProperty("EscalationLevel",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNull();
    }
}
