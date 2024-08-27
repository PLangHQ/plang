using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.FilterModule;

namespace PLangTests.Modules.FilterModule
{

	[TestClass]
	public class ProgramTest
	{


		[TestMethod]
		public void FilterOutProperties()
		{
			var list = new List<dynamic>();
			var obj = new { id = 1, name = "John" };
			var obj2 = new { id = 2, name = "John" };
			list.Add(obj);
			list.Add(obj2);

			var p = new Program();
			var result = (p.FilterOutProperties("$..id", list).Result).ToList();

			Assert.IsNotNull(result);
			Assert.AreEqual(2, result.Count);
			Assert.AreEqual(1, result[0]);
			Assert.AreEqual(2, result[1]);
		}
	}
}
