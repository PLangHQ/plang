using Nethereum.Util;
using Newtonsoft.Json;
using Sprache;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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

		public static string MaxLength(this string txt, int maxLength, string trailing = "...")
		{
			if (txt.Length <= maxLength) return txt;
			return txt.Substring(0, maxLength) + trailing;
		}

		public static string? ComputeHash(this string? input, string mode = "keccak256", string? salt = null)
		{
			if (input == null) return null;

			if (!string.IsNullOrWhiteSpace(salt))
			{
				input = salt + ";" + input;
			}
			if (mode.ToLower() == "sha256") return ComputeSha256(input);
			return ComputeKeccack(input);
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
	}
}
