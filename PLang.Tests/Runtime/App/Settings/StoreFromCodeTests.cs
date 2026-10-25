namespace PLang.Tests.App.Settings;

// The app's store and the settings' store are what the default store provider gives, asked once at first use: a
// provider made default before then is the one they open.
public class StoreFromCodeTests
{
    private sealed class Probe : global::app.store.code.IStore
    {
        public string Name => "probe";
        public bool IsBuiltIn { get; set; }
        public string? Source { get; set; }
        public List<string> Files { get; } = new();

        public global::app.store.@this Open(global::app.type.item.path.@this file, System.Func<bool> memory, global::app.actor.context.@this context)
        {
            Files.Add(file.ToString());
            return new global::app.store.sqlite.@this(file, memory, context);
        }
    }

    [Test]
    public async Task TheStores_AreTheDefaultProvidersOpening()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var probe = new Probe();
        app.Code.Register<global::app.store.code.IStore>(probe);
        await app.Code.SetDefault<global::app.store.code.IStore>("probe").IsSuccess();

        await app.actor.list.System.Setting.Load();
        _ = app.store;

        await Assert.That(probe.Files.Any(file => file.EndsWith("setting/data.sqlite"))).IsTrue();
        await Assert.That(probe.Files.Any(file => file.EndsWith(".data/data.sqlite"))).IsTrue();
    }
}
