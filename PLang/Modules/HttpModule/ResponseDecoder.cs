using System.Text;

namespace PLang.Modules.HttpModule;

/// <summary>
/// Turns the bytes of a text response into a string. The charset of the Content-Type header wins. Without one,
/// bytes that are valid UTF-8 are UTF-8, and only bytes that are not valid UTF-8 are guessed from, because guessing
/// takes valid UTF-8 text with accents (Icelandic: þ, ð, æ, ö) for cp1250 or latin1 and produces mojibake.
/// </summary>
public static class ResponseDecoder
{
	/// <param name="Text">The decoded text, without a byte order mark.</param>
	/// <param name="Encoding">The encoding that was used.</param>
	/// <param name="Source">header, utf8 or guess.</param>
	/// <param name="Details">Only set when the bytes were guessed from: one line per candidate.</param>
	public record Decoded(string Text, Encoding Encoding, string Source, List<string>? Details = null);

	private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	static ResponseDecoder()
	{
		// Make windows-1250, iso-8859-x and the other code pages available by name
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
	}

	public static Decoded Decode(byte[] bytes, string? charsetFromHeader)
	{
		var fromHeader = GetEncoding(charsetFromHeader);
		if (fromHeader != null)
		{
			return new Decoded(GetString(fromHeader, bytes), fromHeader, "header");
		}

		try
		{
			return new Decoded(GetString(StrictUtf8, bytes), Encoding.UTF8, "utf8");
		}
		catch (DecoderFallbackException)
		{
			// not UTF-8
		}

		var charsets = UtfUnknown.CharsetDetector.DetectFromBytes(bytes);
		var enc = charsets.Detected?.Encoding ?? Encoding.UTF8;
		var details = charsets.Details?
			.Select(p => $"EncodingName:{p.EncodingName} - Confidence:{p.Confidence} - Log:{p.StatusLog}")
			.ToList();
		return new Decoded(GetString(enc, bytes), enc, "guess", details);
	}

	/// <summary>The encoding a charset name stands for, null when there is no name or it is unknown. Quotes around the name are removed.</summary>
	public static Encoding? GetEncoding(string? charset)
	{
		charset = charset?.Trim().Trim('"', '\'');
		if (string.IsNullOrEmpty(charset)) return null;

		try
		{
			return Encoding.GetEncoding(charset);
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	// Encoding.GetString keeps a byte order mark as the character U+FEFF at the start of the text
	private static string GetString(Encoding encoding, byte[] bytes)
	{
		var preamble = encoding.GetPreamble();
		int skip = preamble.Length > 0 && bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
		if (skip == 0 && encoding.CodePage == 65001 && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) skip = 3;
		return encoding.GetString(bytes, skip, bytes.Length - skip);
	}
}
