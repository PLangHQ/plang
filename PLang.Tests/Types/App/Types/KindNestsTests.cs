namespace PLang.Tests.App.Types;

/// <summary>
/// A kind nests (ruling 1): a type's kind is itself a type, and that type can carry its
/// own kind. The target shape — a type <c>list</c> whose kind is a <c>list</c> whose kind
/// is <c>path</c> — writes as <c>{list, kind: {list, kind: path}}</c> and navigates as
/// <c>outer.kind.kind.Name == "path"</c>. Lives in C# because today's formal plang
/// written-type grammar (<c>name&lt;kind&gt;</c>) is one level deep, so a plang test can't
/// express the proof without the surface this stage introduces.
/// </summary>
public class KindNestsTests
{
    private readonly global::app.@this _app = new global::app.@this("/test").Testing();

    [After(Test)]
    public async Task Dispose() => await _app.DisposeAsync();

    // Target (ruling 1): a type's kind is itself a type. Constructing `list<list<path>>`
    // today puts the whole "list<path>" string into the kind's Name, so the outer type's
    // kind doesn't separate — kind.Name is "list<path>" rather than "list". After stage 2
    // the kind IS a type and its Name reads as "list" (the inner's own `kind.Name` is "path").
    [Test]
    public async Task AListOfListsOfPath_HasAKindWhoseInnerKindIsPath()
    {
        var ctx = _app.actor.list.User.Context;
        var outer = _app.type.list[new global::app.type.@this("list", "list<path>"), ctx];
        // After stage 2: outer.kind.Name is "list" (the kind is a type, "list" the name of that type).
        // Today: outer.kind.Name is "list<path>" (the kind is a flat string of the whole inner face).
        await Assert.That(outer.kind.Name).IsEqualTo("list");
    }

    // The written form a nested kind round-trips through is `{list, kind: {list, kind: path}}` —
    // Face() renders it, Read() parses it back to the same type.
    // Today `type.@this.ToString()` (type/this.cs:547) renders `name<kind.Name>`, so a nested
    // kind becomes "list<list<path>>" only by the kind's Name already carrying "list<path>"
    // as one glued string — not the structured nest ruling 1 names.
    [Test]
    public async Task TheWrittenForm_RoundTrips_ListOfListOfPath()
    {
        var ctx = _app.actor.list.User.Context;
        var outer = _app.type.list[new global::app.type.@this("list", "list<path>"), ctx];
        // The structured proof: after stage 2 the outer's kind IS a `list` type with its
        // own `path` kind, so a plang-visible "inner kind" reads as "path". Today there is
        // no inner kind — the Name is a glued string — so this reads whatever reflects
        // that absence (empty / the glued string / an error).
        var inner = _app.type.list[new global::app.type.@this(outer.kind.Name), ctx];
        await Assert.That(inner.kind.Name).IsEqualTo("path");
    }
}
