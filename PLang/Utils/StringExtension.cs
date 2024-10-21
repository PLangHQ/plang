using Nethereum.Util;
using Newtonsoft.Json;
using Sprache;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PLang.Errors;
namespace PLang.Utils
{
	public static class StringExtension
	{
		public static string AsVar(this string str)
		{
			return "%" + str.Replace("%", "") + "%";
		}
		public static string ClearWhitespace(this string text)
		{
			if (string.IsNullOrEmpty(text)) return text;

			text = text.Replace("\r", "").Replace("\n", "").Replace("\t", "");
			text = Regex.Replace(text, @"\s*", "");
			return text;
		}
		public static string ToCamelCase(this string txt)
		{
			if (txt.StartsWith("$."))
			{
				return txt.Substring(0, 3).ToLower() + txt.Substring(3);
			}
			return char.ToLowerInvariant(txt[0]) + txt.Substring(1);
		}
		public static string ToTitleCase(this string txt)
		{
			return Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(txt);
		}

		public static string Between(this string content, string startChar, string endChar)
		{
			int idx = content.IndexOf(startChar) + 1;
			int endIdx = content.IndexOf(endChar);
			return content.Substring(idx, endIdx - idx);
		}

		public static string JsonSafe(this string txt)
		{
			string str = JsonConvert.SerializeObject(txt).Remove(0, 1);
			str = str.Remove(str.Length - 1, 1);
			return str;
		}

		public static string MaxLength(this string? txt, int maxLength, string trailing = "...")
		{
			if (txt == null) return "";
			if (txt.Length <= maxLength) return txt;
			return txt.Substring(0, maxLength) + trailing;
		}

		public static (string Hash, IError? Error) ComputeHash(this string? input, string mode = "keccak256", string? salt = null)
		{
			if (input == null) {
				return (string.Empty, new Error("input to compute hash cannog be empty"));
			}

			if (!string.IsNullOrWhiteSpace(salt))
			{
				input = salt + ";" + input;
			}
			if (mode.ToLower() == "sha256") return (ComputeSha256(input), null);
			return (ComputeKeccack(input), null);
		}


		public static string ComputeSha256(this string input)
		{
			if (input == null) return "";

			// Convert the input string to a byte array
			byte[] bytes = Encoding.UTF8.GetBytes(input);
			
			// Create a new instance of the SHA256Managed class
			using (var sha256 = SHA256.Create())
			{
				// Compute the hash of the byte array
				byte[] hashBytes = sha256.ComputeHash(bytes);

				// Convert the hash byte array to a hexadecimal string
				StringBuilder hashStringBuilder = new StringBuilder();
				foreach (byte b in hashBytes)
				{
					hashStringBuilder.Append(b.ToString("x2"));
				}

				return hashStringBuilder.ToString();
			}
		}
		public static string ComputeHmacSha256(this string input, string secretKey, int hashSize = 256)
		{
			if (input == null) return "";

			UTF8Encoding encoding = new UTF8Encoding();
			byte[] secretKeyBytes = encoding.GetBytes(secretKey);
			byte[] signatureBytes = encoding.GetBytes(input);
			string signature = String.Empty;
			HMAC hmac;
			switch(hashSize)
			{		
				case 384:
					hmac = new HMACSHA384(secretKeyBytes);
					break;
				case 512:
					hmac = new HMACSHA512(secretKeyBytes);
					break;
				case 256:
				default:
					hmac = new HMACSHA256(secretKeyBytes);
					break;
			}

			byte[] signatureHash = hmac.ComputeHash(signatureBytes);
			string signatureHex = String.Concat(Array.ConvertAll(signatureHash, x => x.ToString("x2")));
			return signatureHex;
		}

		public static string ComputeKeccack(this string input)
		{
			if (input == null) return "";

			byte[] bytes = Encoding.UTF8.GetBytes(input);
			var keccak = new Sha3Keccack();

			byte[] hashBytes = keccak.CalculateHash(bytes);

			StringBuilder hashStringBuilder = new StringBuilder();
			foreach (byte b in hashBytes)
			{
				hashStringBuilder.Append(b.ToString("x2"));
			}

			return hashStringBuilder.ToString();
		}

		public static string Repeat(this char text, int count)
		{
			return new string(text, count);
		}

		// Convert a string into a slug (for URLs)
		public static string ToSlug(this string input)
		{
			string str = input.ToLower();
			str = Regex.Replace(str, @"[^a-z0-9\s-]", ""); // Remove invalid characters
			str = Regex.Replace(str, @"\s+", "-").Trim(); // Convert whitespaces to dashes
			return str;
		}

		// Extracts all unique words from a string
		public static IEnumerable<string> ExtractUniqueWords(this string input)
		{
			var words = input.Split(new[] { ' ', '.', ',', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
			return words.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		// Abbreviates a string to a specified length, adding an ellipsis if it was shortened
		public static string Abbreviate(this string input, int length)
		{
			if (string.IsNullOrWhiteSpace(input) || input.Length <= length)
				return input;

			return input.Substring(0, length - 3) + "...";
		}

		// Count the number of vowels in a string
		public static int CountVowels(this string input)
		{
			return input.Count(c => "aeiouAEIOU".Contains(c));
		}

		// Extract all numeric sequences from a string
		public static IEnumerable<string> ExtractNumericSequences(this string input)
		{
			return Regex.Matches(input, @"\d+").Select(m => m.Value);
		}

		// Convert a string into a PascalCase (or UpperCamelCase) string
		public static string ToPascalCase(this string input)
		{
			string[] words = input.Split(' ', '-', '_');
			return string.Join("", words.Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower()));
		}

		// Remove everything before the specified input in a string
		public static string RemoveEverythingBefore(this string source, string input)
		{
			if (input.StartsWith(".") || input.StartsWith("#"))
			{

			}

			int index = source.IndexOf(input, StringComparison.OrdinalIgnoreCase);
			return index >= 0 ? source.Substring(index + input.Length) : source;
		}

		// Remove everything after the specified input in a string
		public static string RemoveEverythingAfter(this string source, string input)
		{
			int index = source.IndexOf(input, StringComparison.OrdinalIgnoreCase);
			return index >= 0 ? source.Substring(0, index) : source;
		}

		public static string ClearHtml(this string text)
		{
			if (string.IsNullOrEmpty(text)) return text;

			text = Regex.Replace(text, "<.*?>", " ");
			text = Regex.Replace(text, @"\s{2,}", " ").Trim();
			return text;
		}

		public static string ToBase64(this string text)
		{
			byte[] plainTextBytes = Encoding.UTF8.GetBytes(text);
			return Convert.ToBase64String(plainTextBytes);
		}

		public static int FuzzyMatchScore(this string s1, string s2)
		{
			int score = 0;

			if (s1.Length > s2.Length)
			{
				(s1, s2) = (s2, s1); // Ensure s1 is the shorter string
			}

			for (int i = 0; i <= s2.Length - s1.Length; i++)
			{
				int tempScore = 0;

				for (int j = 0; j < s1.Length; j++)
				{
					if (s1[j] == s2[i + j])
					{
						tempScore += 1;
					}
				}

				score = Math.Max(score, tempScore);
			}

			// Adjust score for matching substrings, common prefixes, etc.
			return score + (s1.StartsWith(s2.Substring(0, Math.Min(3, s2.Length))) ? 2 : 0);
		}

		public static double JaroWinklerDistance(this string s1, string s2)
		{
			double jaroDistance = JaroDistance(s1, s2);
			int prefixLength = 0;

			for (int i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
			{
				if (s1[i] == s2[i])
				{
					prefixLength++;
				}
				else
				{
					break;
				}
			}

			return jaroDistance + (prefixLength * 0.1 * (1 - jaroDistance));
		}

		private static double JaroDistance(this string s1, string s2)
		{
			if (s1 == s2)
			{
				return 1.0;
			}

			int s1Length = s1.Length;
			int s2Length = s2.Length;

			int matchDistance = Math.Max(s1Length, s2Length) / 2 - 1;

			bool[] s1Matches = new bool[s1Length];
			bool[] s2Matches = new bool[s2Length];

			int matches = 0;
			double transpositions = 0.0;

			for (int i = 0; i < s1Length; i++)
			{
				int start = Math.Max(0, i - matchDistance);
				int end = Math.Min(i + matchDistance + 1, s2Length);

				for (int j = start; j < end; j++)
				{
					if (s2Matches[j]) continue;
					if (s1[i] != s2[j]) continue;
					s1Matches[i] = true;
					s2Matches[j] = true;
					matches++;
					break;
				}
			}

			if (matches == 0) return 0.0;

			int k = 0;
			for (int i = 0; i < s1Length; i++)
			{
				if (!s1Matches[i]) continue;
				while (!s2Matches[k]) k++;
				if (s1[i] != s2[k]) transpositions++;
				k++;
			}

			transpositions /= 2.0;

			return ((matches / (double)s1Length) + (matches / (double)s2Length) + ((matches - transpositions) / matches)) / 3.0;
		}
		public static int LevenshteinDistance(this string source, string target)
		{
			if (string.IsNullOrEmpty(source)) return target.Length;
			if (string.IsNullOrEmpty(target)) return source.Length;

			var sourceLength = source.Length;
			var targetLength = target.Length;

			var distance = new int[sourceLength + 1, targetLength + 1];

			for (var i = 0; i <= sourceLength; distance[i, 0] = i++) { }
			for (var j = 0; j <= targetLength; distance[0, j] = j++) { }

			for (var i = 1; i <= sourceLength; i++)
			{
				for (var j = 1; j <= targetLength; j++)
				{
					var cost = (target[j - 1].ToString().Equals(source[i - 1].ToString(), StringComparison.OrdinalIgnoreCase)) ? 0 : 1;
					distance[i, j] = Math.Min(
						Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
						distance[i - 1, j - 1] + cost);
				}
			}

			return distance[sourceLength, targetLength];
		}
	}
}
