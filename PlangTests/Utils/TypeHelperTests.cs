using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using PLang.Utils;
using PLangTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils.Tests
{
	[TestClass()]
	public class TypeHelperTests : BasePLangTest
	{
		[TestInitialize]
		public void Init()
		{
			Initialize();
		}




		[TestMethod()]
		public void GetTypeTest()
		{
			var type = typeHelper.GetRuntimeType("PLang.Modules.CodeModule");

			Assert.IsTrue(typeof(PLang.Modules.CodeModule.Program) == type);
		}

		[TestMethod()]
		public void TryConvertToMatchingTypeTest()
		{
			List<int> list = [ 1, 2, 3, 4 ];
			var jarray = JArray.Parse("[1, 2, 3, 4]");


			(var obj1, var obj2) = TypeHelper.TryConvertToMatchingType(list, jarray);

			Assert.AreEqual(obj1.GetType(), obj2.GetType());

		}
    }
}