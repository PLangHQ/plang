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
	public class StringExtensionTests
	{
		[TestMethod()]
		public void BetweenTest()
		{
			var str = "DataSource=.db/system.sqlite;Version=3";
			var path = str.Between("=", ";");
			Assert.AreEqual(".db/system.sqlite", path);
		}
	}
}