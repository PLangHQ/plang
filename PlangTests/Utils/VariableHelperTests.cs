using LightInject;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using PLang.Building;

using PLang.Runtime;
using PLang.Services.SettingsService;
using PLangTests;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils.Tests
{
    [TestClass()]
	public class VariableHelperTests : BasePLangTest
	{
		[TestInitialize]
		public void Initialize()
		{
			base.Initialize();
		}



		[TestMethod()]
		public void LoadVariablesTest()
		{

			var memoryStack = new Runtime.MemoryStack(pseudoRuntime, engine, settings, variableHelper, contextAccessor);
			memoryStack.Put("name", "John");
			memoryStack.Put("age", 12);
			memoryStack.Put("userInfo", new { address = "Location 32", zip = 662 });

			context.AddOrReplace(ReservedKeywords.MemoryStack, memoryStack);

			var helper = new VariableHelper(settings, logger);
			var text = "Hello %name%, your age is %age%, and you live at %userInfo.address% and %userInfo.zip%, is this correct?";
			text = helper.LoadVariables(memoryStack, text).ToString();

			Assert.AreEqual("Hello John, your age is 12, and you live at Location 32 and 662, is this correct?",
				text);

			var text2 = "%name%%age%%userInfo.address%%userInfo.zip%";
			text2 = helper.LoadVariables(memoryStack, text2).ToString();

			Assert.AreEqual("John12Location 32662",
				text2);
		}

		[TestMethod()]
		public void TestVariableWithMethods()
		{
			// set 'name' as %Name.Trim()%
			var memoryStack = new Runtime.MemoryStack(pseudoRuntime, engine, settings, variableHelper, contextAccessor);
			memoryStack.Put("TestName", " John ");
			memoryStack.Put("name", "%TestName.Trim()%");

			var name = memoryStack.Get("name");
			Assert.AreEqual("John", name);

			var dateTime = DateTime.Now;
			var strDateTime = dateTime.ToString("yyyy-MM-dd");
			memoryStack.Put("today", dateTime);
			var today = memoryStack.Get("today.ToString(\"yyyy-MM-dd\")");
			Assert.AreEqual(strDateTime, today);

			string hello = "hello \"quoted\" world";
			string text = "App says:" + hello;

			memoryStack.Put("txt", text);

			var textBack = memoryStack.Get("txt.JsonSafe()");
			Assert.AreEqual("App says:hello \\\"quoted\\\" world", textBack);

			memoryStack.Put("test", "App is saying hello to you");
			var textReplace = memoryStack.Get(@"test.Replace(""App"", ""PLang"").Replace(""hello"", ""hi"")");
			Assert.AreEqual("PLang is saying hi to you", textReplace);

		}

		[TestMethod()]
		public void LoadVariablesTest2()
		{
			var variableHelper = new VariableHelper(settings, logger);
			var memoryStack = new MemoryStack(pseudoRuntime, engine, settings, variableHelper, contextAccessor);
			memoryStack.Put("text", "world");
			memoryStack.Put("text2", "plang");

			context.AddOrReplace(ReservedKeywords.MemoryStack, memoryStack);
		

			settings.Get<string>(typeof(Settings), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns("mega");

			Dictionary<string, object> dict = new Dictionary<string, object>();
			dict.Add("stuff", "Hello %text%");
			dict.Add("stuff2", "Hello %text2%");
			dict.Add("stuff3", @"This is it %Settings.Get(""key"", """", """")%");
			dict = variableHelper.LoadVariables(memoryStack, dict);

			Assert.AreEqual("Hello world", dict["stuff"]);
			Assert.AreEqual("Hello plang", dict["stuff2"]);
			Assert.AreEqual("This is it mega", dict["stuff3"]);
		}
		[TestMethod()]
		public void LoadVariables_TestContextRervedKeywords()
		{
			var variableHelper = new VariableHelper(settings, logger);
			var memoryStack = new MemoryStack(pseudoRuntime, engine, settings, variableHelper, contextAccessor);
			memoryStack.Put("text", "world");
			memoryStack.Put("text2", "plang");

			context.AddOrReplace(ReservedKeywords.Goal, new { GoalObject = true });
			context.AddOrReplace(ReservedKeywords.MemoryStack, memoryStack);
		


			settings.Get<string>(typeof(Settings), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns("mega");

			Dictionary<string, object> dict = new Dictionary<string, object>();
			dict.Add("stuff", "Hello %text%");
			dict.Add("stuff2", "Hello %text2%");
			dict.Add("stuff3", @"This is it %Settings.Get(""key"", """", """")%");
			dict.Add("goal", "This is %!Goal%");
			dict = variableHelper.LoadVariables(memoryStack, dict);

			Assert.AreEqual("Hello world", dict["stuff"]);
			Assert.AreEqual("Hello plang", dict["stuff2"]);
			Assert.AreEqual("This is it mega", dict["stuff3"]);
			Assert.AreEqual("This is { GoalObject = True }", dict["goal"].ToString().Replace("\n", "").Replace("\r", ""));
		}


		[TestMethod()]
		public void LoadVariables_TestNow()
		{
			DateTime now = DateTime.Now;
			SystemTime.Now = () =>
			{
				return now;
			};

			string content = "Hello %Now%";

			var result = memoryStack.LoadVariables(content, false);

			Assert.AreEqual("Hello " + now.ToString(), result);

			content = "Hello %Now.ToString(\"s\")%";

			result = memoryStack.LoadVariables(content, false);

			Assert.AreEqual("Hello " + now.ToString("s"), result);
		}


		[TestMethod]
		public void LoadVariableInJObject()
		{
			var jObject = new JObject();
			jObject.Add("to", "user@example.org");
			jObject.Add("subject", "hello world");

			memoryStack.Put("domain", "plang");
			memoryStack.Put("request", jObject);

			var jobj = JObject.Parse(@$"{{
				""From"": ""example@example.org"",
				""ReplyTo"": ""%domain%@example.org"",
				""To"": ""%request.to%"",
				""Subject"": ""%request.subject%"",
				""MessageStream"": ""outbound"",
				}}");

			var result = memoryStack.LoadVariables(jobj);

			Assert.IsTrue(result.ToString().Contains("plang@example.org"));
			Assert.IsTrue(result.ToString().Contains("user@example.org"));
			Assert.IsTrue(result.ToString().Contains("hello world"));


		}
	}
}