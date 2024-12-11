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
		public void GetMethodsTest()
		{

			var methods = typeHelper.GetMethodsAsString(typeof(PLang.Modules.DbModule.Program));
			Assert.IsTrue(methods.Contains("Select") && methods.Contains("Insert"));

			methods = typeHelper.GetMethodsAsString(typeof(PLang.Modules.LocalOrGlobalVariableModule.Program));
			Assert.IsTrue(methods.Contains("SetVariable") && methods.Contains("SetDefaultValueOnVariables"));
		}

		[TestMethod()]
		public void GetTypeTest()
		{
			var type = typeHelper.GetRuntimeType("PLang.Modules.CodeModule");

			Assert.IsTrue(typeof(PLang.Modules.CodeModule.Program) == type);
		}

		[TestMethod]
		public void GetMethodNamesAsString_Test()
		{
			var methods = typeHelper.GetMethodNamesAsString(typeof(Modules.OutputModule.Program));
			int i = 0;

		}


		[TestMethod]
		public void GetMethodNamesDescription_Test()
		{
			var methodInfo = TypeHelper.GetMethodDescription(typeof(Modules.OutputModule.Program), "Write");
			int i = 0;

		}
	}
}