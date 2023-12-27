using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils.Tests
{
	[TestClass()]
	public class DateTimeExtensionTests
	{
		[TestMethod()]
		public void GetUnixTimeTest()
		{
			var now = DateTime.Now;
			var result = now.GetUnixTime();
			Assert.IsTrue(result > 0);

			var start = new DateTime(1970, 1, 1);
			Assert.AreEqual(0, start.GetUnixTime());
		}
	}
}