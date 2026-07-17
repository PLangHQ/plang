using System.Text.RegularExpressions;

namespace ObpScan;

/// <summary>A member's NAME — owns its own smell judgment. A name knows whether it is a
/// compound (a phrase where a type wants to exist) and whether it carries a verb.
/// The verb lexicon lives here because "am I a verb-name?" is the name's own question.</summary>
public sealed class MemberName
{
    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Compute","Split","Get","Set","Run","Build","Make","Find","Resolve","Render","Validate",
        "Parse","Convert","Create","Wrap","Nest","Load","Save","Merge","Clone","Dispatch","Describe",
        "Discover","Register","Apply","Emit","Format","Normalize","Populate","Group","Sort","Fold",
        "Read","Write","Open","Close","Insert","Copy","Index","Handle","Orchestrate"
    };

    private readonly string _raw;
    private readonly bool _boolish;
    private readonly bool _contract;   // interface impl / base override — the NAME is not ours to judge
    public IReadOnlyList<string> Words { get; }

    public MemberName(string raw, bool boolish, bool contract)
    {
        _raw = raw;
        _boolish = boolish;
        _contract = contract;
        // an explicit-interface name arrives dotted (System.Collections.IEnumerable.X) — keep the tail
        var bare = raw.Contains('.') ? raw[(raw.LastIndexOf('.') + 1)..] : raw;
        // Async is a suffix convention, not a word — RunAsync is the single verb Run
        if (bare.EndsWith("Async", StringComparison.Ordinal) && bare.Length > 5) bare = bare[..^5];
        Words = Regex.Matches(bare.TrimStart('@'), "[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])")
                     .Select(m => m.Value).Where(s => s.Length > 0).ToList();
    }

    /// <summary>A single word is clean (a verb naming intent, or a noun). More than one word is a
    /// compound — the smell — unless it's a boolean Is/Has (a sanctioned exemption, still noted).
    /// A contract member is never OUR smell — the interface/base named it.</summary>
    public bool IsCompound => !_contract && Words.Count > 1;
    private bool CarriesVerb => Words.Any(Verbs.Contains);

    /// <summary>The name's own verdict cell for the scan row.</summary>
    public string Flag
    {
        get
        {
            if (_contract) return "contract";
            if (Words.Count <= 1) return "clean";
            var shown = string.Join("·", Words);
            if (_boolish && Words[0] is "Is" or "Has") return $"compound-bool ({shown})";
            return CarriesVerb ? $"**VERB+NOUN** ({shown})" : $"**COMPOUND** ({shown})";
        }
    }

    public override string ToString() => _raw;
}
