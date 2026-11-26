using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building;
using PLang.Building.Model;
using System.Collections.Generic;

namespace PLang.Building.Tests
{
	[TestClass()]
	public class RetryDelayFromTextTests
	{
		private static ErrorHandler Handler(int count, int? delay) => new ErrorHandler { Key = "NotConnected", RetryHandler = new RetryHandler { RetryCount = count, RetryDelayInMilliseconds = delay } };

		[DataTestMethod]
		[DataRow("- tell %a% to X, on error NotConnected call Connect, retry 3 times over 1 minute", 3, 20000)]
		[DataRow("- tell %a% to X, on error call Connect, retry 100 times over 10 minutes", 100, 6000)]
		[DataRow("- tell %a% to X, on error call Connect, retry 3 times over 30 seconds", 3, 10000)]
		[DataRow("- tell %a% to X, on error call Connect, retry 1000 times over 24 hours", 1000, 86400)]
		public void Over_SpreadsTheTriesOverTheTime(string text, int count, int expectedDelay)
		{
			var handlers = new List<ErrorHandler> { Handler(count, 60000) };
			StepBuilder.SetRetryDelayFromText(text, handlers);
			Assert.AreEqual(expectedDelay, handlers[0].RetryHandler!.RetryDelayInMilliseconds);
		}

		[TestMethod]
		public void NoOver_LeavesTheDelay()
		{
			var handlers = new List<ErrorHandler> { Handler(3, 20000) };
			StepBuilder.SetRetryDelayFromText("- x, on error call Y, retry 3 times, wait 20 seconds between retries", handlers);
			Assert.AreEqual(20000, handlers[0].RetryHandler!.RetryDelayInMilliseconds);
		}
	}
}
