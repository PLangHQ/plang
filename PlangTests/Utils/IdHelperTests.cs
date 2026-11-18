using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;

namespace PLang.Utils.Tests
{
	[TestClass()]
	public class IdHelperTests
	{
		[TestMethod()]
		public void NewId_InATightLoop_IsUnique()
		{
			var ids = new HashSet<long>();
			for (int i = 0; i < 100_000; i++)
			{
				Assert.IsTrue(ids.Add(IdHelper.NewId()), $"Duplicate id after {i} ids");
			}
		}

		[TestMethod()]
		public void NewId_FromManyThreads_IsUnique()
		{
			var ids = new ConcurrentBag<long>();
			Parallel.For(0, 8, _ =>
			{
				for (int i = 0; i < 50_000; i++) ids.Add(IdHelper.NewId());
			});
			Assert.AreEqual(ids.Count, ids.Distinct().Count());
		}

		[TestMethod()]
		public void NewId_MoreThanOneMillisecondsWorth_DoesNotThrow()
		{
			// 4096 ids fit in one millisecond; past that the generator waits for the next one instead of throwing.
			for (int i = 0; i < 20_000; i++) IdHelper.NewId();
		}

		[TestMethod()]
		public void NewId_Increases()
		{
			long previous = IdHelper.NewId();
			for (int i = 0; i < 10_000; i++)
			{
				long next = IdHelper.NewId();
				Assert.IsTrue(next > previous);
				previous = next;
			}
		}
	}
}
