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

    // a URL's path counts case on every OS
    [Test] public async Task AUrlGrant_KeepsCase()
    {
        await Assert.That(Allows("https://x.com/Admin", MatchMode.exact, "https://x.com/admin")).IsFalse();
        await Assert.That(Allows("https://x.com/Admin/*", MatchMode.glob, "https://x.com/admin/a")).IsFalse();
    }

    // a glob compares as an exact grant does: on a case-blind disk, case aside
    [Test, RunOn(TUnit.Core.Enums.OS.Windows)] public async Task OnWindows_AGlobGrant_IgnoresCase()
        => await Assert.That(Allows(@"C:\Docs\*.txt", MatchMode.glob, @"c:\docs\A.TXT")).IsTrue();

    // a glob folds case as the exact grant does, and no wider: to OrdinalIgnoreCase the Kelvin sign (U+212A) is not k
    // and the long s (U+017F) is not s, so a glob on k or s never opens them
    [Test, RunOn(TUnit.Core.Enums.OS.Windows)] public async Task OnWindows_AGlobGrant_FoldsAsTheExactGrantDoes()
    {
        await Assert.That(Allows(@"C:\apps\kdir\*", MatchMode.glob, "C:\\apps\\\u212Adir\\x")).IsFalse();
        await Assert.That(Allows(@"C:\apps\sdir\*", MatchMode.glob, "C:\\apps\\\u017Fdir\\x")).IsFalse();
        await Assert.That(Allows(@"C:\apps\kdir\*", MatchMode.glob, @"C:\APPS\KDIR\x")).IsTrue();
    }

    // a ** crosses parts wherever it stands: at the start, in the middle, at the end
    [Test] public async Task TwoStars_CrossPartsAtStartMiddleAndEnd()
    {
        await Assert.That(Allows("**/x.txt", MatchMode.glob, "/a/b/x.txt")).IsTrue();
        await Assert.That(Allows("/a/**/x.txt", MatchMode.glob, "/a/b/c/x.txt")).IsTrue();
        await Assert.That(Allows("/a/**/x.txt", MatchMode.glob, "/a/b/c/y.txt")).IsFalse();
        await Assert.That(Allows("/a/**", MatchMode.glob, "/a/b/c")).IsTrue();
        await Assert.That(Allows("/a/**", MatchMode.glob, "/b/a/c")).IsFalse();
    }

    // a ? is exactly one character, never none, never two, never a separator
    [Test] public async Task AQuestionMark_IsExactlyOneCharacter()
    {
        await Assert.That(Allows("/a?c", MatchMode.glob, "/abc")).IsTrue();
        await Assert.That(Allows("/a?c", MatchMode.glob, "/ac")).IsFalse();
        await Assert.That(Allows("/a?c", MatchMode.glob, "/abbc")).IsFalse();
    }

    [Test] public async Task SeveralStars_InOnePart()
    {
        await Assert.That(Allows("/a/*.*", MatchMode.glob, "/a/x.y.z")).IsTrue();
        await Assert.That(Allows("/a/*.*", MatchMode.glob, "/a/xyz")).IsFalse();
        await Assert.That(Allows("/a/*x*", MatchMode.glob, "/a/bxc")).IsTrue();
        await Assert.That(Allows("/a/*x*", MatchMode.glob, "/a/b/xc")).IsFalse();
    }

    // a * may be empty, so it takes an empty part; the folder itself is not under it, and a part after it is not in it
    [Test] public async Task AStar_EmptyPartTrailingSeparatorAndTheFolderItself()
    {
        await Assert.That(Allows("/a/*/b", MatchMode.glob, "/a//b")).IsTrue();
        await Assert.That(Allows("/a/*", MatchMode.glob, "/a/b/")).IsFalse();
        await Assert.That(Allows("/a/*", MatchMode.glob, "/a")).IsFalse();
        await Assert.That(Allows("/a/**", MatchMode.glob, "/a")).IsFalse();
        await Assert.That(Allows("/a/**", MatchMode.glob, "/a/")).IsTrue();
    }

    // the literals beside a * hold on both sides
    [Test] public async Task TheLiteralsBesideAStar()
    {
        await Assert.That(Allows("/a*b", MatchMode.glob, "/ab")).IsTrue();
        await Assert.That(Allows("/a*b", MatchMode.glob, "/axb")).IsTrue();
        await Assert.That(Allows("/a*b", MatchMode.glob, "/ax/b")).IsFalse();
        await Assert.That(Allows("/a*b", MatchMode.glob, "/abx")).IsFalse();
        await Assert.That(Allows("/a*b", MatchMode.glob, "/xab")).IsFalse();
    }

    // many stars against a long candidate that almost matches answer at once: the matcher never backtracks
    [Test] public async Task ManyStars_OnALongCandidate_AnswerAtOnce()
    {
        var grant = "/" + string.Concat(Enumerable.Repeat("*a", 40)) + "*b";
        var place = "/" + new string('a', 20_000);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await Assert.That(Allows(grant, MatchMode.glob, place)).IsFalse();
        await Assert.That(clock.Elapsed).IsLessThan(System.TimeSpan.FromSeconds(2));
    }

    [Test, ExcludeOn(TUnit.Core.Enums.OS.Windows | TUnit.Core.Enums.OS.MacOs)] public async Task OnLinux_AnExactGrant_KeepsCase()
        => await Assert.That(Allows("/docs/a.txt", MatchMode.exact, "/Docs/a.txt")).IsFalse();
}
