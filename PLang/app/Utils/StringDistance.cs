namespace app.Utils;

public static class StringDistance
{
    /// <summary>
    /// Levenshtein distance between two strings. Lower = more similar.
    /// </summary>
    public static int Levenshtein(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }

    /// <summary>
    /// Sorts candidates by similarity to the target. Closest match first.
    /// </summary>
    public static IEnumerable<string> OrderBySimilarity(string target, IEnumerable<string> candidates)
    {
        return candidates.OrderBy(c => Levenshtein(target, c));
    }
}
