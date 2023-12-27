using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils.Tests
{
	[TestClass()]
	public class StringHelperTests
	{
		[TestMethod()]
		public void ConvertToStringTest()
		{
			int i = 1;

			string result = StringHelper.ConvertToString(i);
			Assert.AreEqual("1", result);

			string str = "hello";
			result = StringHelper.ConvertToString(str);
			Assert.AreEqual(str, result);

			string json = @"{""name"": ""Ble""}";
			result = StringHelper.ConvertToString(json);
			Assert.AreEqual(json, result);

			json = @"{
  ""name"": ""Ble""
}";
			var jObject = JObject.Parse(json);
			result = StringHelper.ConvertToString(jObject);
			Assert.AreEqual(json, result);

		}
	}
}