using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Models.ObjectValueConverters;
using PLang.Runtime;
using System.Collections;
using System.Linq;

namespace PLang.Models.ObjectValueExtractors.Tests
{
	[TestClass()]
	public class MethodExtractorTests
	{
		private static object? Call(object value, string method)
		{
			var parent = new ObjectValue("s", value);
			var extractor = new MethodExtractor(value, parent);
			return extractor.Extract(new PathSegment(method, SegmentType.Method))?.Value;
		}

		[TestMethod()]
		public void Split_WithTextSeparator_UsesTheOptionalOptionsDefault()
		{
			var result = Call("a b c", "Split(\" \")") as IEnumerable;
			CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result!.Cast<object>().Select(p => p.ToString()).ToArray());
		}

		[TestMethod()]
		public void Split_WithNewLineEscape_SplitsOnNewLine()
		{
			var result = Call("one\ntwo", "Split(\"\\n\")") as IEnumerable;
			CollectionAssert.AreEqual(new[] { "one", "two" }, result!.Cast<object>().Select(p => p.ToString()).ToArray());
		}

		[TestMethod()]
		public void Replace_TwoTextArguments()
		{
			Assert.AreEqual("a-b", Call("a b", "Replace(\" \", \"-\")"));
		}
	}
}
