using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace PLang.Models.ObjectValueExtractors.Tests
{
	[TestClass()]
	public class TryGetIndexTests
	{
		[TestMethod()]
		public void Numbers_OfAnyType_AreIndexes()
		{
			Assert.IsTrue(ObjectValueExtractor.TryGetIndex(1, out var i1)); Assert.AreEqual(1, i1);
			Assert.IsTrue(ObjectValueExtractor.TryGetIndex(2L, out var i2)); Assert.AreEqual(2, i2);
			Assert.IsTrue(ObjectValueExtractor.TryGetIndex(3.0, out var i3)); Assert.AreEqual(3, i3);
			Assert.IsTrue(ObjectValueExtractor.TryGetIndex(4m, out var i4)); Assert.AreEqual(4, i4);
			Assert.IsTrue(ObjectValueExtractor.TryGetIndex(new JValue(5L), out var i5)); Assert.AreEqual(5, i5);
			Assert.IsTrue(ObjectValueExtractor.TryGetIndex("6", out var i6)); Assert.AreEqual(6, i6);
		}

		[TestMethod()]
		public void NotANumber_IsNoIndex()
		{
			Assert.IsFalse(ObjectValueExtractor.TryGetIndex(null, out _));
			Assert.IsFalse(ObjectValueExtractor.TryGetIndex("idx", out _));
			Assert.IsFalse(ObjectValueExtractor.TryGetIndex(1.5, out _));
			Assert.IsFalse(ObjectValueExtractor.TryGetIndex(long.MaxValue, out _));
		}
	}
}
