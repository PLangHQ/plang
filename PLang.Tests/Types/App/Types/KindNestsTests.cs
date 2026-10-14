using System.Text.Json;

namespace PLang.Tests.App.Types;

/// <summary>
/// A kind nests (ruling 1): a type's kind is itself a type, so <c>{list, kind: {list, kind: path}}</c>
/// is the written form of a list of lists of path. The proof is a round-trip through the type's own
/// serializer: a Data carrying this nested-kind Type serializes to the structured JSON and
/// deserializes back into a type that still carries the inner kind — no flattening, no walking
/// through <c>kind.Name</c>. Today the type serializer's reader (<c>type/serializer/Reader.cs</c>)
/// reads <c>kind</c> as a bare string (line 33: <c>reader.String()</c>), so a nested-kind JSON
/// trips at the inner <c>{</c>.
/// </summary>
public class KindNestsTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/kindnests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.data.@this FromJson(string json)
        => _app.actor.list.User.Context.Format("application/plang").Deserialize(json, _app.actor.list.User.Context);

    // The structured JSON for a Data whose Type is `list<list<path>>` — the nested-kind form the plan names.
    private const string NestedJson = "{\"name\":\"grid\",\"type\":{\"name\":\"list\",\"kind\":{\"name\":\"list\",\"kind\":\"path\"}},\"value\":[[\"a.txt\"]]}";

    // Read the nested-kind form through the type's own wire. After stage 2 the reader
    // recurses into the inner kind and the resulting Type carries both levels: outer is
    // list, outer's kind is itself a list, and that inner kind is path. Today reader.String()
    // on the inner `{` throws, so the deserialize itself fails.
    [Test]
    public async Task TheNestedWrittenForm_RoundTripsThroughTheTypeSerializer()
    {
        var read = FromJson(NestedJson);
        await Assert.That(read.Type?.Name).IsEqualTo("list");
        await Assert.That(read.Type?.kind).IsAssignableTo<global::app.type.@this>();
    }
}
