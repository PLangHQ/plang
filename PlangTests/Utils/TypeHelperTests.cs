using Microsoft.VisualStudio.TestTools.UnitTesting;
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
		public void Init() {
			Initialize();
		}


		

		[TestMethod()]
		public void GetTypeTest()
		{
			var type = typeHelper.GetRuntimeType("PLang.Modules.CodeModule");

			Assert.IsTrue(typeof(PLang.Modules.CodeModule.Program) == type);
		}

		
	}
}