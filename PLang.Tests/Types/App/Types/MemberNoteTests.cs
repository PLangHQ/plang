using note = global::app.type.item.note.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// A type's member's notes are read as an action's are, line by line: a line for the member itself (its one-liner and
/// how a step says it), one per argument, and <c>Returns</c>; what the notes and the member's arguments don't agree on,
/// as warnings. The notes here are text's, staged in the app's own <c>system/type/text</c>.
/// </summary>
public class MemberNoteTests
{
    private string _tempDir = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_member_note_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_tempDir, "system", "type", "text"));
        _app = new global::app.@this(_tempDir).Testing();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private void Stage(string member, string body)
        => System.IO.File.WriteAllText(System.IO.Path.Combine(_tempDir, "system", "type", "text", $"{member}.notes.md"), body);

    // the member's notes, read through their own door
    private async Task<note> Read(string member)
    {
        var notes = _app.type.list["text"].Property!.Single(p => p.Name == member).Note(Ctx)!;
        await new global::app.data.@this("note", notes, context: Ctx).Value();
        return notes;
    }

    [Test]
    public async Task AMethodsNotes_NameItself_ItsArguments_AndReturns()
    {
        Stage("replace",
            "replace — The text with every occurrence of one substring swapped for another. · say: %text.replace(\"old\", \"new\")%\n" +
            "old — the substring to find · say: the first argument\n" +
            "new — what to put in its place · say: the second argument\n" +
            "Returns — the text with every match replaced.");

        var notes = await Read("replace");

        await Assert.That(notes.Line.Select(line => line.Name?.ToString())).IsEquivalentTo(new[] { "replace", "old", "new", "Returns" });
        await Assert.That(notes.Line[0].Say?.ToString()).IsEqualTo("%text.replace(\"old\", \"new\")%");
        await Assert.That(notes.Returns?.Prose.ToString()).IsEqualTo("the text with every match replaced.");
        await Assert.That(notes.Warning.Count).IsEqualTo(0);
    }

    // a member read as it is (a property) has no arguments: its own line and Returns, nothing owed
    [Test]
    public async Task APropertysNotes_AreItsOwnLineAndReturns()
    {
        Stage("length", "length — How many characters the text holds. · say: %text.length%\nReturns — a number.");

        var notes = await Read("length");

        await Assert.That(notes.Line.Count).IsEqualTo(2);
        await Assert.That(notes.Warning.Count).IsEqualTo(0);
    }

    // a line naming none of its arguments, and an argument no line names, are what don't agree
    [Test]
    public async Task ALineNamingNoArgument_AndAnArgumentWithoutALine_AreWarnings()
    {
        Stage("replace", "replace — swaps · say: %text.replace(\"a\", \"b\")%\nold — the substring\ncolour — red\nReturns — the text.");

        var notes = await Read("replace");

        await Assert.That(notes.Warning.Select(w => w.Key)).IsEquivalentTo(new[] { "NoteWithoutArgument", "ArgumentWithoutNote" });
        await Assert.That(notes.Warning.Select(w => w.Message)).Contains("text.replace: the notes line 'colour' names no argument");
        await Assert.That(notes.Warning.Select(w => w.Message)).Contains("text.replace: the argument 'new' has no notes line");
    }
}
