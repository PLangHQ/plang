using System.Reflection;
using AppService = global::app.service.@this;

namespace PLang.Tests.App.ChannelsTests;

// Flat App.Services collection + Service type + Actor cleanup.

public class Stage7_AppServicesTests
{
    [Test]
    public async Task Services_NewWithParent_CreatesService_AddsToCollection()
    {
        await using var app = new global::app.@this("/tmp/s7a");
        var s = app.Services.New(parent: app.User);
        await Assert.That(s.Parent).IsEqualTo(app.User);
        await Assert.That(app.Services.Count).IsEqualTo(1);
        await Assert.That(app.Services.Contains(s)).IsTrue();
    }

    [Test]
    public async Task Service_Channels_IsEmptyOnConstruction()
    {
        await using var app = new global::app.@this("/tmp/s7b");
        var s = app.Services.New(parent: app.User);
        await Assert.That(s.Channels.ChannelNames.Any()).IsFalse();
    }

    [Test]
    public async Task Service_Identity_NavigatesToAppSystemIdentity()
    {
        await using var app = new global::app.@this("/tmp/s7c");
        app.System.Identity = new global::app.module.identity.Identity { Name = "system-id" };
        var s = app.Services.New(parent: app.User);
        await Assert.That(s.Identity).IsEqualTo(app.System.Identity);
    }

    [Test]
    public async Task Service_Parent_IsTheActorPassedAtCreation()
    {
        await using var app = new global::app.@this("/tmp/s7d");
        var sUser = app.Services.New(parent: app.User);
        var sSystem = app.Services.New(parent: app.System);
        await Assert.That(sUser.Parent).IsEqualTo(app.User);
        await Assert.That(sSystem.Parent).IsEqualTo(app.System);
    }

    [Test]
    public async Task Service_AwaitUsing_RemovesFromCollection_AndDisposesChannels()
    {
        await using var app = new global::app.@this("/tmp/s7e");
        AppService captured;
        global::app.channel.@this disposedCh;
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
        await using var app = new global::app.@this("/tmp/s7f");
        await using var sA = app.Services.New(parent: app.User);
        await using var sB = app.Services.New(parent: app.User);
        sA.Channels.Register(StreamChannel.Memory("input"));
        sB.Channels.Register(StreamChannel.Memory("input"));
        await Assert.That(sA.Channels.Get("input")).IsNotEqualTo(sB.Channels.Get("input"));
    }

    [Test]
    public async Task ActorChoices_DropsToUserAndSystem()
    {
        var values = global::app.actor.@this.Choices(null);
        await Assert.That(values).Contains("user");
        await Assert.That(values).Contains("system");
        await Assert.That(values).DoesNotContain("service");
    }

    [Test]
    public async Task Services_ConcurrentNewAndRemove_NoServiceDropped()
    {
        // Regression for the ConcurrentBag drain-and-rebuild Remove that could lose
        // services racing with concurrent New(). With ConcurrentDictionary<Guid, Service>,
        // Add and Remove are atomic, so every still-live service must remain visible.
        await using var app = new global::app.@this("/tmp/s7-race");
        const int n = 200;
        var pool = new AppService[n];
        for (int i = 0; i < n; i++) pool[i] = app.Services.New(parent: app.User);

        // Half are removed concurrently; the other half stay alive and must remain.
        var removeTasks = new Task[n / 2];
        for (int i = 0; i < n / 2; i++)
        {
            var s = pool[i];
            removeTasks[i] = Task.Run(async () => await s.DisposeAsync());
        }
        // Concurrently spawn another batch — these should also all land.
        var addTasks = new Task<AppService>[n / 2];
        for (int i = 0; i < n / 2; i++)
            addTasks[i] = Task.Run(() => app.Services.New(parent: app.User));

        await Task.WhenAll(removeTasks);
        var added = await Task.WhenAll(addTasks);

        // Survivors: original second half (n/2) + newly added (n/2) = n.
        await Assert.That(app.Services.Count).IsEqualTo(n);
        for (int i = n / 2; i < n; i++)
            await Assert.That(app.Services.Contains(pool[i])).IsTrue();
        foreach (var s in added)
            await Assert.That(app.Services.Contains(s)).IsTrue();
    }

    [Test]
    public async Task Actor_NoLongerHasEscalationLevel()
    {
        var prop = typeof(global::app.actor.@this).GetProperty("EscalationLevel",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNull();
    }
}
