using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Grant = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.Runtime.App.Settings;

/// An actor's own setting (its grants, its identities) is read from its row only when the row is signed — as its owner's
/// code saves it. A row written below plang without its signature (the store's file edited) is refused on read, in the
/// Store view too: never a grant nobody asked for.
public class OwnRowSignatureTests : System.IAsyncDisposable
{
    private readonly string root;
    private readonly string secret;

    public OwnRowSignatureTests()
    {
        root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-ownrow-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        var outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-ownrow-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        secret = System.IO.Path.Combine(outside, "secret.txt");
        System.IO.File.WriteAllText(secret, "outside");
    }

    public System.Threading.Tasks.ValueTask DisposeAsync() => default;

    private string Store => System.IO.Path.Combine(root, ".data", "setting", "data.sqlite");
    private const string Row = "user!app.actor.permission.setting";

    // the user's persisted grant on the secret file, saved by its owner's door (signed), in a first run
    private async Task Granted()
    {
        await using var first = new global::app.@this(root).TestSigning();
        var user = first.actor.list.User;
        await user.Permission.Add(new global::app.data.@this<Grant>("", Grant.Request(user, secret, Verb.read), context: user.Context), persist: true);
        await Assert.That(await user.Permission.Find(user.Context.File(secret), Verb.read)).IsNotNull();
    }

    // the row's signature layer taken off below plang: its data layer written in its place
    private void Unsigned()
    {
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Store}"))
        {
            connection.Open();
            var read = connection.CreateCommand();
            read.CommandText = "SELECT data FROM [settings] WHERE key = @key";
            read.Parameters.AddWithValue("@key", Row);
            var wire = System.Text.Json.Nodes.JsonNode.Parse((string)read.ExecuteScalar()!)!.AsObject();
            var layer = wire["@schema"]?.GetValue<string>() == "signature" ? wire["value"]!.ToJsonString() : wire.ToJsonString();
            var write = connection.CreateCommand();
            write.CommandText = "UPDATE [settings] SET data = @data WHERE key = @key";
            write.Parameters.AddWithValue("@key", Row);
            write.Parameters.AddWithValue("@data", layer);
            write.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }

    [Test] public async Task ASignedOwnRow_IsReadBack()
    {
        await Granted();
        await using var next = new global::app.@this(root).TestSigning();
        var user = next.actor.list.User;
        await Assert.That(await user.Permission.Find(user.Context.File(secret), Verb.read)).IsNotNull();
    }

    [Test] public async Task AnOwnRowWithoutItsSignature_IsRefusedOnRead()
    {
        await Granted();
        Unsigned();
        await using var next = new global::app.@this(root).TestSigning();
        var user = next.actor.list.User;

        var read = await user.Setting.Get("app.actor.permission.setting");
        await read.IsFailure();
        await Assert.That(read.Error!.Key).IsEqualTo("SettingUnsigned");
    }
}
