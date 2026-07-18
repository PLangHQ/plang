using System.Text.RegularExpressions;

namespace ObpScan;

/// <summary>A member's NAME — owns its own smell judgment AND the fix it suggests. A name knows
/// whether it carries a verb (behaviour that wants a type), is plural (should be singular), or ends
/// in a redundant category-suffix (Number/Type/Name/…) it should shed. The lexicons live here
/// because "what kind of name am I, and what should I be" is the name's own question.</summary>
public sealed class MemberName
{
    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Compute","Split","Get","Set","Run","Build","Make","Find","Resolve","Render","Validate",
        "Parse","Convert","Create","Wrap","Nest","Load","Save","Merge","Clone","Dispatch","Describe",
        "Discover","Register","Apply","Emit","Format","Normalize","Populate","Group","Sort","Fold",
        "Read","Write","Open","Close","Insert","Copy","Index","Handle","Orchestrate",
        "Reflect","Unwrap","Strip","Judge","Scan","Collect","Attach","Stamp"
    };

    // trailing category-words that restate a type/kind the name already IS — shed them (Ingi 2026-07-17)
    private static readonly HashSet<string> Redundant = new(StringComparer.OrdinalIgnoreCase)
    {
        "Number","Type","Name","Kind","Category","Info","Data","State","Value","Index","Text"
    };

    private readonly string _raw;
    private readonly bool _boolish;
    private readonly bool _contract;
    public IReadOnlyList<string> Words { get; }

    public MemberName(string raw, bool boolish, bool contract)
    {
        _raw = raw;
        _boolish = boolish;
        _contract = contract;
        var bare = raw.Contains('.') ? raw[(raw.LastIndexOf('.') + 1)..] : raw;
        if (bare.EndsWith("Async", StringComparison.Ordinal) && bare.Length > 5) bare = bare[..^5];
        Words = Regex.Matches(bare.TrimStart('@'), "[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])")
                     .Select(m => m.Value).Where(s => s.Length > 0).ToList();
    }

    /// <summary>Flagged for the summary tally — any name the scan wants a human to look at (not a
    /// contract/boolean/clean single word).</summary>
    public bool IsFlagged => Flag is not ("contract" or "clean") && !Flag.StartsWith("bool");

    private bool IsPlural => Plural(Words.Count == 0 ? "" : Words[^1]);
    private bool HasVerb => Words.Any(Verbs.Contains);

    /// <summary>The name's own verdict cell — and, where it can, the name it should be.</summary>
    public string Flag
    {
        get
        {
            if (_contract) return "contract";
            var shown = string.Join("·", Words);
            // the ONLY name exemption is a boolean PREFIXED Is/Has (docs). A boolean named with Is/Has
            // anywhere else (HeadIs) is Noun+Verb — still the smell; suggest moving the marker to the front.
            if (_boolish && Words.Count > 0 && Words[0] is "Is" or "Has") return $"bool ({shown})";
            if (_boolish && Words.Contains("Is"))
                return $"**BOOL-SUFFIX** ({shown}) → Is{string.Concat(Words.Where(w => w != "Is"))}";
            // a verb-bearing compound is BEHAVIOUR first — it wins over a trailing plural noun
            // (SplitAtConditions is a Split method, not a "Conditions" collection).
            if (Words.Count > 1 && HasVerb) return $"**VERB+NOUN** ({shown})";
            if (IsPlural) return $"**PLURAL** ({shown}) → {Singular()}";
            if (Words.Count <= 1) return "clean";
            if (Stripped() is { } s) return $"**REDUNDANT** ({shown}) → {s}";
            return $"compound ({shown})";
        }
    }

    // ends in a plural 's' — the LLM reader dismisses a normal word that just ends in s (Status, …)
    private static bool Plural(string w)
        => w.Length > 2 && w.EndsWith("s", StringComparison.OrdinalIgnoreCase)
           && !w.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
           && !w.EndsWith("us", StringComparison.OrdinalIgnoreCase)
           && !w.EndsWith("is", StringComparison.OrdinalIgnoreCase)
           && !w.EndsWith("as", StringComparison.OrdinalIgnoreCase);

    private string Singular()
    {
        var w = _raw.TrimStart('@');
        if (w.EndsWith("ies", StringComparison.OrdinalIgnoreCase)) return w[..^3] + "y";
        return w.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? w[..^1] : w;
    }

    // strip trailing redundant category-words while a real word remains → the name it should be
    private string? Stripped()
    {
        var words = Words.ToList();
        bool shed = false;
        while (words.Count > 1 && Redundant.Contains(words[^1])) { words.RemoveAt(words.Count - 1); shed = true; }
        return shed ? string.Concat(words) : null;
    }

    public override string ToString() => _raw;
}
