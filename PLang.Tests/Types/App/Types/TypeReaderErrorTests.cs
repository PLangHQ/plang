namespace PLang.Tests.App.Types;

/// <summary>
/// A bad value from a peer lands as its own error: a type's reader, handed json it can't make its value from, declines
/// with the type's own error (a DeclinedException, which the read answers softly on its binding) — never a raw
/// .NET exception a catch further up turns into a generic failure.
/// </summary>
public class TypeReaderErrorTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/type-reader-error-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // a type's reader at the json's first token: what it read, or what it threw
    private (global::app.type.item.@this? Read, System.Exception? Thrown) Read(global::app.type.reader.ITypeReader type, string json)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var tokens = new System.Text.Json.Utf8JsonReader(bytes);
        tokens.Read();
        var reader = new global::app.type.item.kind.json.Reader(tokens, bytes);
        try { return (type.Read(ref reader, null, new global::app.type.reader.ReadContext(_app.actor.list.User.Context)), null); }
        catch (System.Exception ex) { return (null, ex); }
    }

    private static async Task Declined((global::app.type.item.@this? Read, System.Exception? Thrown) read, string key)
    {
        await Assert.That(read.Thrown).IsTypeOf<global::app.error.DeclinedException>().Because(read.Thrown?.GetType().FullName ?? "nothing was thrown");
        await Assert.That(((global::app.error.DeclinedException)read.Thrown!).Error.Key).IsEqualTo(key);
    }

    private static global::app.type.item.choice.serializer.Reader<global::app.module.cache.type.cache> Choice() => new("cache");

    [Test] public async Task ABadSize_IsDeclinedAsASize()
        => await Declined(Read(new global::app.type.item.size.serializer.Reader(), "\"lots\""), "SizeInvalid");

    [Test] public async Task ABadParallel_IsDeclinedAsAParallel()
        => await Declined(Read(new global::app.type.item.parallel.serializer.Reader(), "{\"threads\":4}"), "ParallelInvalid");

    [Test] public async Task ABadChoice_IsDeclinedAsAChoice()
        => await Declined(Read(Choice(), "\"sometimes\""), "ChoiceInvalid");

    [Test] public async Task GoodValues_Read()
    {
        await Assert.That(Read(new global::app.type.item.size.serializer.Reader(), "\"2 MiB\"").Read?.ToString()).IsEqualTo("2 MiB");
        await Assert.That(Read(new global::app.type.item.parallel.serializer.Reader(), "{\"cpu\":4}").Thrown).IsNull();
        await Assert.That(Read(Choice(), "\"skip\"").Read?.ToString()).IsEqualTo("skip");
    }

    // the choice courier answers its decline on the data, its error whole
    [Test] public async Task ABadChoice_ThroughItsCourier_FailsItsData()
    {
        var context = _app.actor.list.User.Context;
        var data = new global::app.data.@this("cache", context: context);
        var made = global::app.type.item.choice.@this<global::app.module.cache.type.cache>.Create("sometimes", null, data);
        await Assert.That(made).IsNull();
        await Assert.That(data.Error?.Key).IsEqualTo("ChoiceInvalid");
    }
}
