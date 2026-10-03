using note = global::app.type.item.note.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// An action's notes are read once, line by line (decision 411): a named line <c>Name — prose · say: … · builder: …</c>
/// into its fields; every other line, and every line inside a code fence, kept free, as written, in order; the
/// <c>Returns</c> line found; and what the notes and the action's properties don't agree on, as warnings. The notes
/// here are <c>file.list</c>'s (Path, Pattern, Recursive), staged in the app's own <c>system/modules</c>.
/// </summary>
public class ActionNoteTests
{
    private string _tempDir = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_note_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_tempDir, "system", "modules", "file"));
        _app = new global::app.@this(_tempDir).Testing();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
    }

    private void Stage(string body)
        => System.IO.File.WriteAllText(System.IO.Path.Combine(_tempDir, "system", "modules", "file", "list.notes.md"), body);

    private global::app.goal.step.action.@this List => _app.Module("file")!["list"]!;

    // The notes, read through their own door.
    private async Task<note> Read()
    {
        var notes = List.Note;
        await new global::app.data.@this("note", notes, context: _app.actor.list.User.Context).Value();
        return notes;
    }

    [Test]
    public async Task ANamedLine_IsReadIntoItsFields()
    {
        Stage("Pattern — which files come back, as a glob · say: `matching '<glob>'` · builder: only when the step names one\n");

        var line = (await Read()).Line[0];

        await Assert.That(line.Name?.ToString()).IsEqualTo("Pattern");
        await Assert.That(line.Prose.ToString()).IsEqualTo("which files come back, as a glob");
        await Assert.That(line.Say?.ToString()).IsEqualTo("`matching '<glob>'`");
        await Assert.That(line.Builder?.ToString()).IsEqualTo("only when the step names one");
    }

    [Test]
    public async Task FreeLines_StayAsWritten_InOrder_AndALineInAFenceNamesNothing()
    {
        Stage("Only when the step names a folder.\nPath — the folder · say: x · not a tag\n```json\nRecursive — inside a fence\n```\nReturns — a list.\n");

        var notes = await Read();

        await Assert.That(notes.Line.Select(line => line.Name?.ToString() ?? "-")).IsEquivalentTo(
            new[] { "-", "Path", "-", "-", "-", "Returns", "-" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(notes.Line[0].Prose.ToString()).IsEqualTo("Only when the step names a folder.");
        await Assert.That(notes.Line[1].Prose.ToString()).IsEqualTo("the folder · not a tag");
        await Assert.That(notes.Line[3].Prose.ToString()).IsEqualTo("Recursive — inside a fence");
        await Assert.That(notes.Returns!.Prose.ToString()).IsEqualTo("a list.");
    }

    [Test]
    public async Task Warning_NamesALineWithNoProperty_AndEachPropertyWithNoLine()
    {
        Stage("Path — the folder\nPatern — a typo\nReturns — a list.\n");

        var warning = (await Read()).Warning;

        await Assert.That(warning.Select(w => w.Message)).IsEquivalentTo(new[]
        {
            "file.list: the notes line 'Patern' names no property",
            "file.list: the property 'Pattern' has no notes line",
            "file.list: the property 'Recursive' has no notes line",
        });
    }

    [Test]
    public async Task NoNotesFile_HasNoLines_AndEveryPropertyWarns()
    {
        var notes = await Read();

        await Assert.That(notes.Line.Count).IsEqualTo(0);
        await Assert.That(notes.Warning.Select(w => w.Key)).IsEquivalentTo(new[] { "PropertyWithoutNote", "PropertyWithoutNote", "PropertyWithoutNote" });
    }

    // A goal reaches the warnings by navigating — `%action.Note.Warning%` — never through the notes' door first.
    [Test]
    public async Task NavigatingToTheWarnings_ReadsTheNotesFirst()
    {
        Stage("Path — the folder\n");
        var action = new global::app.data.@this("action", List, context: _app.actor.list.User.Context);

        var warning = await (await action.Get("Note")).Get("Warning");

        await Assert.That(warning.Success).IsTrue();
        await Assert.That((await warning.Value())!.EnumerateItems(_app.actor.list.User.Context).Count()).IsEqualTo(2);
    }
}
