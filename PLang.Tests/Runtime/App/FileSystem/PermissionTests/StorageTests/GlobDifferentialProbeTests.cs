using System.Text.RegularExpressions;
using PermissionRecord = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace PLang.Tests.App.FileSystem.PermissionTests.StorageTests;

/// <summary>Security probe (windows SW-1): the hand-written glob matcher answers as the regex it replaced on Linux (one
/// separator, ordinal), over random globs and candidates — nothing it matches wider, nothing narrower. Seeded, so a red is
/// reproducible. Not Windows (the regex model there is the separator-aware one the matcher itself defines).</summary>
public class GlobDifferentialProbeTests
{
    private static bool OldRegex(string pattern, string candidate)
    {
        var rx = new System.Text.StringBuilder("^");
        for (int i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*') { if (i + 1 < pattern.Length && pattern[i + 1] == '*') { rx.Append(".*"); i++; } else rx.Append("[^/]*"); }
            else if (c == '?') rx.Append("[^/]");
            else rx.Append(Regex.Escape(c.ToString()));
        }
        return Regex.IsMatch(candidate, rx.Append('$').ToString());
    }

    [Test, ExcludeOn(TUnit.Core.Enums.OS.Windows)]
    public async Task TheMatcher_AnswersAsTheRegexItReplaced()
    {
        await using var app = new global::app.@this("/tmp/globdiff-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();
        var actor = app.actor.list.User;
        var random = new Random(20261026);
        const string patternAlphabet = "ab/.*?*A";
        const string candidateAlphabet = "ab/.A";
        string Make(Random r, string alphabet, int max) => new(Enumerable.Range(0, r.Next(0, max)).Select(_ => alphabet[r.Next(alphabet.Length)]).ToArray());

        var differences = new List<string>();
        for (var i = 0; i < 20000 && differences.Count < 5; i++)
        {
            var pattern = "/" + Make(random, patternAlphabet, 8);
            var candidate = "/" + Make(random, candidateAlphabet, 10);
            var now = new PermissionRecord(actor.Name, pattern, PermissionRecord.AllVerbs, MatchMode.glob, app.type.list[typeof(PermissionRecord)])
                .Allows(PermissionRecord.Request(actor, candidate, Verb.read));
            if (now != OldRegex(pattern, candidate)) differences.Add($"{pattern} vs {candidate}: now {now}");
        }
        await Assert.That(differences).IsEmpty();
    }
}
