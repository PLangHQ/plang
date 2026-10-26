using PermissionRecord = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.StorageTests;

// A grant matches a place as the OS names it: a glob's * and ? stay inside one part — they stop at each of the OS's
// separators, only ** crosses — and an exact grant compares as the path does (case aside where the OS's disk is).
public class GrantMatchTests
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/grantmatch-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();

    private bool Allows(string grant, MatchMode match, string place)
    {
        var actor = _app.actor.list.User;
        return new PermissionRecord(actor.Name, grant, PermissionRecord.AllVerbs, match, _app.type.list[typeof(PermissionRecord)])
            .Allows(PermissionRecord.Request(actor, place, Verb.read));
    }

    private static readonly char Separator = System.IO.Path.DirectorySeparatorChar;

    [Test] public async Task AStar_StaysInOnePart()
    {
        await Assert.That(Allows($"{Separator}apps{Separator}*{Separator}file.txt", MatchMode.glob, $"{Separator}apps{Separator}a{Separator}file.txt")).IsTrue();
        await Assert.That(Allows($"{Separator}apps{Separator}*{Separator}file.txt", MatchMode.glob, $"{Separator}apps{Separator}a{Separator}b{Separator}file.txt")).IsFalse();
    }

    [Test] public async Task TwoStars_CrossParts()
        => await Assert.That(Allows($"{Separator}apps{Separator}**", MatchMode.glob, $"{Separator}apps{Separator}a{Separator}b{Separator}file.txt")).IsTrue();

    [Test] public async Task AQuestionMark_IsOneCharacterOfAPart()
        => await Assert.That(Allows($"{Separator}a?c", MatchMode.glob, $"{Separator}a{Separator}c")).IsFalse();

    // on Windows "\" and "/" both separate, so neither may be crossed by a *
    [Test, RunOn(TUnit.Core.Enums.OS.Windows)] public async Task OnWindows_AStar_StopsAtEitherSeparator()
    {
        await Assert.That(Allows(@"C:\apps\*\file.txt", MatchMode.glob, @"C:\apps\a\b\file.txt")).IsFalse();
        await Assert.That(Allows(@"C:\apps\*\file.txt", MatchMode.glob, @"C:\apps\a/b\file.txt")).IsFalse();
    }

    // a case-blind disk names one place in any case: an exact grant on it opens it whichever case the request has
    [Test, RunOn(TUnit.Core.Enums.OS.Windows)] public async Task OnWindows_AnExactGrant_IgnoresCase()
        => await Assert.That(Allows(@"C:\Docs\a.txt", MatchMode.exact, @"c:\docs\A.TXT")).IsTrue();

    [Test, ExcludeOn(TUnit.Core.Enums.OS.Windows | TUnit.Core.Enums.OS.MacOs)] public async Task OnLinux_AnExactGrant_KeepsCase()
        => await Assert.That(Allows("/docs/a.txt", MatchMode.exact, "/Docs/a.txt")).IsFalse();
}
