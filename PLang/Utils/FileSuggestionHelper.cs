namespace PLang.Utils;

using PLang.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class FileSuggestionHelper
{
	// Builds a helpful not-found message with typo-friendly suggestions.
	// - filesInFolder: file names or full paths from the target folder
	// - requestedPath: the file path/name the caller requested
	// - includeFullPathInList: include full paths in suggestions (handy if duplicates exist)
	public static string? BuildNotFoundMessage(
		IPLangFileSystem fileSystem,
		string requestedPath,
		int maxSuggestions = 5,
		double minScore = 0.55,
		bool includeFullPathInList = false)
	{
		var dir = fileSystem.Path.GetDirectoryName(requestedPath);
		if (!fileSystem.Directory.Exists(dir)) return $"Directory '{dir}' could not be found.";

		var filesInFolder = fileSystem.Directory.GetFiles(dir);


		var files = (filesInFolder ?? Enumerable.Empty<string>())
			.Where(f => !string.IsNullOrWhiteSpace(f))
			// Keep all entries; do NOT distinct by OrdinalIgnoreCase so we don’t lose potential duplicates
			.ToList();

		var requestedName = Path.GetFileName(requestedPath ?? string.Empty);
		var reqStem = GetStem(requestedName);
		var reqExt = GetExtLower(requestedName);
		var reqNormStem = NormalizeForSimilarity(reqStem);

		var entries = files.Select(f =>
		{
			var name = Path.GetFileName(f);
			var stem = GetStem(name);
			return new FileEntry
			{
				Full = f,
				Name = name,
				Stem = stem,
				NormStem = NormalizeForSimilarity(stem),
				Ext = GetExtLower(name)
			};
		}).ToList();

		var sb = new StringBuilder();
		sb.Append($"File '{requestedName}' was not found.");

		if (entries.Count == 0)
			return sb.ToString();

		// Case-only mismatch(s): exists with same name ignoring case, but casing differs
		var caseOnly = entries
			.Where(e => string.Equals(e.Name, requestedName, StringComparison.OrdinalIgnoreCase)
						&& !string.Equals(e.Name, requestedName, StringComparison.Ordinal))
			.Select(e => includeFullPathInList ? e.Full : e.Name)
			.Distinct(StringComparer.Ordinal) // keep different casings
			.Take(5)
			.ToList();

		if (caseOnly.Count > 0)
		{
			sb.Append(" A file exists with the same name but different casing: ");
			sb.Append(string.Join(", ", caseOnly.Select(n => $"'{n}'")));
			sb.Append('.');
		}

		// Same stem, different extension (incl. extension equivalents)
		var sameStemDiffExt = entries
			.Where(e => string.Equals(e.Stem, reqStem, StringComparison.OrdinalIgnoreCase)
						&& !string.Equals(e.Ext, reqExt, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(e => AreExtensionsEquivalent(e.Ext, reqExt)) // show near-equivalents first
			.ThenBy(e => e.Ext)
			.Select(e => includeFullPathInList ? e.Full : e.Name)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(5)
			.ToList();

		if (sameStemDiffExt.Count > 0)
		{
			sb.Append(" Found files with the same name but a different extension: ");
			sb.Append(string.Join(", ", sameStemDiffExt.Select(n => $"'{n}'")));
			sb.Append('.');
		}

		// Ranked similarity suggestions (case-insensitive, normalized)
		// Prefilter: skip candidates whose normalized stem length differs too much (cheap guard)
		int reqLen = reqNormStem.Length;
		var candidates = entries.Where(e =>
		{
			int len = e.NormStem.Length;
			int diff = Math.Abs(len - reqLen);
			return diff <= Math.Max(6, reqLen / 2); // tuneable
		});

		var ranked = candidates
			.Where(e => !string.Equals(e.Name, requestedName, StringComparison.Ordinal))
			.Select(e => new
			{
				Entry = e,
				Score = ScoreNameSimilarity(requestedName, reqStem, reqNormStem, reqExt, e)
			})
			.OrderByDescending(x => x.Score)
			.Where(x => x.Score >= minScore)
			.Take(maxSuggestions)
			.Select(x => includeFullPathInList ? x.Entry.Full : x.Entry.Name)
			.ToList();

		if (ranked.Count > 0)
		{
			sb.Append(" Did you mean:");
			foreach (var r in ranked)
			{
				sb.Append($"\n - {r}");
			}
		}

		return sb.ToString();
	}

	// Optional: resolve the actual file path ignoring case (useful on Linux/macOS).
	// Returns the matched path or null if none.
	public static string TryResolveCaseInsensitivePath(IEnumerable<string> filesInFolder, string requestedPath)
	{
		var requestedName = Path.GetFileName(requestedPath ?? string.Empty);
		if (string.IsNullOrEmpty(requestedName)) return null;

		return (filesInFolder ?? Enumerable.Empty<string>())
			.FirstOrDefault(f =>
				string.Equals(Path.GetFileName(f), requestedName, StringComparison.OrdinalIgnoreCase));
	}

	// ----------------- internals -----------------

	private sealed class FileEntry
	{
		public string Full { get; set; } = "";
		public string Name { get; set; } = "";
		public string Stem { get; set; } = "";
		public string NormStem { get; set; } = "";
		public string Ext { get; set; } = "";
	}

	private static double ScoreNameSimilarity(
		string requestedName,
		string reqStem,
		string reqNormStem,
		string reqExt,
		FileEntry cand)
	{
		// Normalized Damerau–Levenshtein on normalized stems (case-insensitive, punctuation-insensitive)
		var stemSim = NormalizedDamerauSimilarity(reqNormStem, cand.NormStem);

		// Common prefix ratio on normalized stems (helps "pathsToFile" vs "pathToFile")
		var prefixSim = CommonPrefixRatio(reqNormStem, cand.NormStem);

		// Extension weighting
		double extBonus = 0.0;
		if (string.Equals(reqExt, cand.Ext, StringComparison.OrdinalIgnoreCase))
			extBonus += 0.15;
		else if (AreExtensionsEquivalent(reqExt, cand.Ext))
			extBonus += 0.08;

		// Case-only boost if the names match ignoring case
		double caseBoost =
			string.Equals(requestedName, cand.Name, StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(requestedName, cand.Name, StringComparison.Ordinal)
			? 0.05 : 0.0;

		// Length penalty to avoid very lopsided matches
		var lenMax = Math.Max(reqStem.Length, cand.Stem.Length);
		var lenMin = Math.Min(reqStem.Length, cand.Stem.Length);
		double lengthPenalty = (lenMax > 0 && (lenMax - lenMin) > lenMax * 0.5) ? 0.10 : 0.0;

		var score = 0.75 * stemSim + 0.20 * prefixSim + extBonus + caseBoost - lengthPenalty;
		if (score < 0) score = 0;
		if (score > 1) score = 1;
		return score;
	}

	private static string GetStem(string fileName)
	{
		if (string.IsNullOrEmpty(fileName)) return string.Empty;
		return Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
	}

	private static string GetExtLower(string fileName)
	{
		if (string.IsNullOrEmpty(fileName)) return string.Empty;
		var ext = Path.GetExtension(fileName);
		if (string.IsNullOrEmpty(ext)) return string.Empty;
		return ext.TrimStart('.').ToLowerInvariant();
	}

	// Normalize for similarity: lowercase and drop separators to make "pathToFile", "path_to-file", "path.to file" comparable
	private static string NormalizeForSimilarity(string s)
	{
		if (string.IsNullOrEmpty(s)) return string.Empty;
		Span<char> buf = s.ToLowerInvariant().ToCharArray();
		var list = new List<char>(buf.Length);
		foreach (var ch in buf)
		{
			if (char.IsLetterOrDigit(ch)) list.Add(ch);
		}
		return new string(list.ToArray());
	}

	private static double NormalizedDamerauSimilarity(string a, string b)
	{
		if (a == null) a = string.Empty;
		if (b == null) b = string.Empty;
		if (a.Length == 0 && b.Length == 0) return 1.0;

		int dist = DamerauLevenshteinDistance(a, b);
		int maxLen = Math.Max(a.Length, b.Length);
		return maxLen == 0 ? 1.0 : 1.0 - (double)dist / maxLen;
	}

	private static int DamerauLevenshteinDistance(string s, string t)
	{
		int n = s.Length;
		int m = t.Length;
		var d = new int[n + 1, m + 1];

		for (int i = 0; i <= n; i++) d[i, 0] = i;
		for (int j = 0; j <= m; j++) d[0, j] = j;

		for (int i = 1; i <= n; i++)
		{
			for (int j = 1; j <= m; j++)
			{
				int cost = s[i - 1] == t[j - 1] ? 0 : 1;

				int deletion = d[i - 1, j] + 1;
				int insertion = d[i, j - 1] + 1;
				int substitution = d[i - 1, j - 1] + cost;
				int val = Math.Min(Math.Min(deletion, insertion), substitution);

				if (i > 1 && j > 1 && s[i - 1] == t[j - 2] && s[i - 2] == t[j - 1])
				{
					// transposition
					val = Math.Min(val, d[i - 2, j - 2] + 1);
				}

				d[i, j] = val;
			}
		}
		return d[n, m];
	}

	private static double CommonPrefixRatio(string a, string b)
	{
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
		int max = Math.Max(a.Length, b.Length);
		int len = Math.Min(a.Length, b.Length);
		int i = 0;
		while (i < len && a[i] == b[i]) i++;
		return max == 0 ? 0.0 : (double)i / max;
	}

	// Treat near-equivalent extensions as the same family
	private static bool AreExtensionsEquivalent(string a, string b)
	{
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
		return string.Equals(CanonicalExt(a), CanonicalExt(b), StringComparison.OrdinalIgnoreCase);
	}

	private static string CanonicalExt(string ext)
	{
		ext = (ext ?? string.Empty).ToLowerInvariant();
		return ext switch
		{
			"htm" => "html",
			"jpg" => "jpeg",
			"jpeg" => "jpeg",
			"yml" => "yaml",
			"yaml" => "yaml",
			"tif" => "tiff",
			"tiff" => "tiff",
			_ => ext
		};
	}
}