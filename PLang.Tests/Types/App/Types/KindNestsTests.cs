using System.Text.Json;

namespace PLang.Tests.App.Types;

/// <summary>
/// A kind nests (ruling 1): a type's kind is itself a type, so <c>{list, kind: {list, kind: path}}</c>
/// is the written form of a list of lists of path. The proof is reading that nested-kind JSON
/// through the type's own serializer: the outer is <c>list</c>, the kind is itself a type whose
/// name is <c>list</c>, and that inner type's own kind is <c>path</c> — no flattening, no walking
/// through <c>kind.Name</c>. Today the type serializer's reader (<c>type/serializer/Reader.cs</c>)
/// reads <c>kind</c> as a bare string (line 33: <c>reader.String()</c>), so a nested-kind JSON
/// trips at the inner <c>{</c>.
/// </summary>
public class KindNestsTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/kindnests-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // A hand-written wire string is the at-rest (Store) form — read it so, as plang keeps it; Out would
    // refuse an unsigned Data (that is the cross-actor contract, not what this reads).
    private global::app.data.@this FromJson(string json)
        => _app.actor.list.User.Context.Format("application/plang").Stored(json, _app.actor.list.User.Context);

    // The structured JSON for a Data whose Type is `list<list<path>>` — the nested-kind form the plan names.
    private const string NestedJson = "{\"name\":\"grid\",\"type\":{\"name\":\"list\",\"kind\":{\"name\":\"list\",\"kind\":\"path\"}},\"value\":[[\"a.txt\"]]}";

    // Reads the nested-kind form through the type's own wire. After stage 2 the reader
    // recurses into the inner kind and the resulting Type carries both levels: outer is
    // list, outer's kind is itself a `list` type, and THAT type's own kind is `path`.
    // Today reader.String() on the inner `{` throws, so the deserialize fails at the
    // reader itself — the point where the recursion must land.
    [Test]
    public async Task TheNestedWrittenForm_ReadsThroughTheTypeSerializer()
    {
        var read = FromJson(NestedJson);
        await Assert.That(read.Type?.Name).IsEqualTo("list");

        // The inner kind must be a type.@this (not a kind.@this). Cast through object so the
        // test compiles today: today `.kind` is `type.kind.@this`, and asserting against
        // `type.@this` would be a compile-time clash — the cast makes today's non-match
        // fall at runtime (IsAssignableTo returns false) rather than at compile time.
        var innerKind = read.Type?.kind as object as global::app.type.@this;
        await Assert.That(innerKind).IsNotNull();
        await Assert.That(innerKind!.Name).IsEqualTo("list");
        await Assert.That(innerKind.kind.Name).IsEqualTo("path");
    }
}
