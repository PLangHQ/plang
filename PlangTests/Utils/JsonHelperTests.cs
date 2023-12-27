using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Interfaces;
using PLangTests;
using PLangTests.Mocks;
using System.IO.Abstractions.TestingHelpers;

namespace PLang.Utils.Tests
{
	[TestClass()]
	public class JsonHelperTests : BasePLangTest
	{

		[TestInitialize]
		public void Init()
		{
			base.Initialize();
		}

		record ParseTest(string name);

		[TestMethod()]
		public void TryParseTest()
		{
			string content = @"{""name"":""Micheal""}";
			var result = JsonHelper.TryParse<ParseTest>(content);
			Assert.AreEqual("Micheal", result.name);

			var typeResult = JsonHelper.TryParse<int>("1");
			Assert.AreEqual(1, typeResult);
		}


		[TestMethod()]
		public void IsJsonTest()
		{
			var isJson = JsonHelper.IsJson(@"{""ble"":1}");
			Assert.IsTrue(isJson);

			var isJson2 = JsonHelper.IsJson(@"[{""ble"":1}]");
			Assert.IsTrue(isJson2);

			var isNotJson = JsonHelper.IsJson(@"q");
			Assert.IsFalse(isNotJson);

			var isNotJson2 = JsonHelper.IsJson(@"{""ble""");
			Assert.IsFalse(isNotJson2);

		}

		[TestMethod()]
		public void ParseFilePathTest()
		{
			string path = @"c:\file.json";
			var fileSystem = (PLangMockFileSystem) container.GetInstance<IPLangFileSystem>();
			fileSystem.AddFile(@"c:\file.json", new MockFileData(@"{""name"":""jlk""}"));


			var result = JsonHelper.ParseFilePath<ParseTest>((IPLangFileSystem) fileSystem, path);

			Assert.AreEqual("jlk", result.name);
		}
	}
}