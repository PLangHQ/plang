namespace app.type.item.path;

/// <summary>
/// A path glob: whether it covers a candidate, for file paths (<c>/apps/*/file.txt</c>) and URLs
/// (<c>https://api.example.com/*</c>) alike. <c>*</c> is any run within one part; <c>**</c> any run across parts; <c>?</c>
/// one character of a part. A part ends at each of the OS's separators (<c>/</c> on Linux; <c>\</c> and <c>/</c> on
/// Windows). The literal runs between wildcards compare with the glob's comparison, so a glob never folds case wider than
/// its holder does. Held by a permission's glob grant and by a goal pattern.
/// </summary>
internal sealed class Glob(string text, System.StringComparison comparison)
{
    /// <summary>Whether this glob covers <paramref name="candidate"/>. One pass per piece of the glob over the places the
    /// candidate has reached: no backtracking.</summary>
    internal bool Matches(string candidate)
    {
        var reach = new bool[candidate.Length + 1];
        reach[0] = true;
        for (int at = 0; at < text.Length;)
        {
            var next = new bool[candidate.Length + 1];
            if (text[at] == '*' && at + 1 < text.Length && text[at + 1] == '*')
            {
                // across parts: every place from the first one reached
                var from = System.Array.IndexOf(reach, true);
                if (from < 0) return false;
                for (int p = from; p <= candidate.Length; p++) next[p] = true;
                at += 2;
            }
            else if (text[at] == '*')
            {
                // within a part: a reached place, or one more character that isn't a separator
                for (int p = 0; p <= candidate.Length; p++)
                    next[p] = reach[p] || (p > 0 && next[p - 1] && !IsSeparator(candidate[p - 1]));
                at++;
            }
            else if (text[at] == '?')
            {
                for (int p = 0; p < candidate.Length; p++)
                    if (reach[p] && !IsSeparator(candidate[p])) next[p + 1] = true;
                at++;
            }
            else
            {
                var end = text.IndexOfAny(['*', '?'], at);
                var literal = text.AsSpan(at, (end < 0 ? text.Length : end) - at);
                for (int p = 0; p + literal.Length <= candidate.Length; p++)
                    if (reach[p] && candidate.AsSpan(p, literal.Length).Equals(literal, comparison)) next[p + literal.Length] = true;
                at += literal.Length;
            }
            reach = next;
        }
        return reach[candidate.Length];

        static bool IsSeparator(char c)
            => c == global::app.Utils.PathHelper.DirectorySeparatorChar || c == global::app.Utils.PathHelper.AltDirectorySeparatorChar;
    }
}
