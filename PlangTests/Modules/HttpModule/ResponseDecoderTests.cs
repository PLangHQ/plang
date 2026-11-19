using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.HttpModule;
using System.Text;

namespace PLangTests.Modules.HttpModule
{
	[TestClass]
	public class ResponseDecoderTests
	{
		// Icelandic text with the letters that a guess takes for another code page
		private const string Icelandic = "Þórður Ægisson, Öræfajökull og Ísland: þetta er íslenskur texti með ð, æ og ö.";

		[TestMethod]
		public void Decode_ValidUtf8WithoutCharset_IsUtf8()
		{
			var decoded = ResponseDecoder.Decode(Encoding.UTF8.GetBytes(Icelandic), null);

			Assert.AreEqual(Icelandic, decoded.Text);
			Assert.AreEqual("utf8", decoded.Source);
			Assert.IsNull(decoded.Details);
		}

		[TestMethod]
		public void Decode_ShortValidUtf8_IsNotGuessedAsAnotherCodePage()
		{
			// A single accented word is where a statistical guess is weakest
			foreach (var word in new[] { "Þ", "ð", "æö", "Íslenska", "Ægir", "Þórður" })
			{
				var decoded = ResponseDecoder.Decode(Encoding.UTF8.GetBytes(word), null);
				Assert.AreEqual(word, decoded.Text);
			}
		}

		[TestMethod]
		public void Decode_CharsetInHeader_Wins()
		{
			var latin1 = Encoding.GetEncoding("iso-8859-1");
			var decoded = ResponseDecoder.Decode(latin1.GetBytes("Þórður Ægisson"), "iso-8859-1");

			Assert.AreEqual("Þórður Ægisson", decoded.Text);
			Assert.AreEqual("header", decoded.Source);
		}

		[TestMethod]
		public void Decode_Windows1250InHeader_IsDecodedAsDeclared()
		{
			var cp1250 = Encoding.GetEncoding(1250);
			var decoded = ResponseDecoder.Decode(cp1250.GetBytes("Łódź"), "windows-1250");

			Assert.AreEqual("Łódź", decoded.Text);
		}

		[TestMethod]
		public void Decode_QuotedCharsetInHeader_IsUnquoted()
		{
			var decoded = ResponseDecoder.Decode(Encoding.UTF8.GetBytes(Icelandic), "\"utf-8\"");

			Assert.AreEqual(Icelandic, decoded.Text);
			Assert.AreEqual("header", decoded.Source);
		}

		[TestMethod]
		public void Decode_UnknownCharsetInHeader_FallsBackToUtf8ForValidBytes()
		{
			var decoded = ResponseDecoder.Decode(Encoding.UTF8.GetBytes(Icelandic), "no-such-charset");

			Assert.AreEqual(Icelandic, decoded.Text);
			Assert.AreEqual("utf8", decoded.Source);
		}

		[TestMethod]
		public void Decode_BytesThatAreNotUtf8AndNoCharset_AreGuessed()
		{
			var latin1 = Encoding.GetEncoding("iso-8859-1");
			var bytes = latin1.GetBytes("Þetta er íslenskur texti með mörgum stöfum sem eru ekki ASCII: þ ð æ ö á é í ó ú ý.");

			var decoded = ResponseDecoder.Decode(bytes, null);

			Assert.AreEqual("guess", decoded.Source);
			Assert.IsNotNull(decoded.Details);
			Assert.IsTrue(decoded.Text.Length > 0);
		}

		[TestMethod]
		public void Decode_Utf8ByteOrderMark_IsNotPartOfTheText()
		{
			var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("{\"a\":1}")).ToArray();

			var withoutCharset = ResponseDecoder.Decode(bytes, null);
			var withCharset = ResponseDecoder.Decode(bytes, "utf-8");

			Assert.AreEqual("{\"a\":1}", withoutCharset.Text);
			Assert.AreEqual("{\"a\":1}", withCharset.Text);
		}

		[TestMethod]
		public void Decode_EmptyBody_IsEmptyText()
		{
			Assert.AreEqual("", ResponseDecoder.Decode(Array.Empty<byte>(), null).Text);
			Assert.AreEqual("", ResponseDecoder.Decode(Array.Empty<byte>(), "utf-8").Text);
		}
	}
}
