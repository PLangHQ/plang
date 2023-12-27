using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Org.BouncyCastle.Asn1.Cmp;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Utils;
using PLangTests;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace PLang.Runtime.Tests
{
	[TestClass()]
	public class MemoryStackTests : BasePLangTest
	{
		[TestInitialize]
		public void Init() {
			base.Initialize();
		}

		public class DataTestClass
		{
			public int Number { get; set; }
			public string Title { get; set; }
			public DateTime Date { get; set; }

		}

		public record DataTestRecord(int value);

		[TestMethod]
		public void GetVariableExecutionPlan_Test()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.Put("data", "1");
			var plan = stack.GetVariableExecutionPlan("data", false);

			Assert.AreEqual(plan.VariableName, "data");
			Assert.AreEqual(plan.Calls.Count, 0);

			Assert.AreEqual(plan.Index, 0);
			Assert.AreEqual(plan.ObjectValue.Value, "1");

			//testClass
			//testClass.Property
			//testClass.Name.ToUpper().ToLower()
			//date.ToString("G")
			//rows[0].Name
			//rows[idx].Email

			stack.Put("dataTest", new DataTestClass() {  Number = 1, Title = "Hello", Date = DateTime.Now});
			plan = stack.GetVariableExecutionPlan("dataTest", false);

			var testClass = plan.ObjectValue.Value as DataTestClass;
			Assert.AreEqual(plan.VariableName, "dataTest");
			Assert.AreEqual(testClass.Number, 1);
			Assert.AreEqual(plan.Calls.Count, 0);
			Assert.AreEqual(plan.Index, 0);

			plan = stack.GetVariableExecutionPlan("dataTest.Title", false);
			testClass = plan.ObjectValue.Value as DataTestClass;
			Assert.AreEqual(plan.VariableName, "dataTest");
			Assert.AreEqual(plan.Calls.Count, 1);
			Assert.AreEqual(plan.Calls[0], "Title");
			Assert.AreEqual(testClass.Number, 1);

			plan = stack.GetVariableExecutionPlan("dataTest.Title.ToUpper()", false);

			Assert.AreEqual(plan.VariableName, "dataTest");
			Assert.AreEqual(plan.Calls.Count, 2);
			Assert.AreEqual(plan.Calls[0], "Title");
			Assert.AreEqual(plan.Calls[1], "ToUpper()");
			testClass = plan.ObjectValue.Value as DataTestClass;

			Assert.AreEqual(testClass.Title, "Hello");

			plan = stack.GetVariableExecutionPlan("dataTest.Date.ToString(\"G\")", false);

			Assert.AreEqual(plan.VariableName, "dataTest");
			Assert.AreEqual(plan.Calls.Count, 2);
			Assert.AreEqual(plan.Calls[0], "Date");
			Assert.AreEqual(plan.Calls[1], "ToString(\"G\")");
			testClass = plan.ObjectValue.Value as DataTestClass;

			Assert.AreEqual(testClass.Title, "Hello");

			var list = new List<DataTestClass>();
			list.Add(new DataTestClass() { Number = 1, Title = "Hello", Date = DateTime.Now });
			list.Add(new DataTestClass() { Number = 2, Title = "Hello2", Date = DateTime.Now.AddDays(1) });
			stack.Put("dataTestList", list);

			plan = stack.GetVariableExecutionPlan("dataTestList[2].Date.ToString(\"d\")", false);

			Assert.AreEqual(plan.VariableName, "dataTestList");
			Assert.AreEqual(plan.Calls.Count, 2);
			Assert.AreEqual(plan.Calls[0], "Date");
			Assert.AreEqual(plan.Calls[1], "ToString(\"d\")");
			Assert.AreEqual(plan.Index, 2);

			stack.Put("dataTestList", list);

			var list2 = stack.Get("dataTestList") as List<DataTestClass>;
			
			Assert.AreEqual(list2[1].Title, "Hello2");

			stack.Put("idx", 2);
			plan = stack.GetVariableExecutionPlan("dataTestList[idx].Date.ToString(\"d\")", false);

			Assert.AreEqual(plan.VariableName, "dataTestList");
			Assert.AreEqual(plan.Calls.Count, 2);
			Assert.AreEqual(plan.Calls[0], "Date");
			Assert.AreEqual(plan.Calls[1], "ToString(\"d\")");
			Assert.AreEqual(plan.Index, 2);

			var list3 = stack.Get("dataTestList") as List<DataTestClass>;
			Assert.AreEqual(list3[1].Title, "Hello2");

			var dict = new Dictionary<string, object>();
			dict.Add("Word1", "Nr1");
			dict.Add("Word2", "Nr2");
			dict.Add("Word3", "Nr3");
			stack.Put("Dict", dict);

			var dictValue = stack.Get("Dict[\"Word2\"]");
			Assert.AreEqual("Nr2", dictValue);

			var dictValue2 = stack.Get("Dict[Word2]");
			Assert.AreEqual("Nr2", dictValue2);

			stack.Put("KeyInDict", "Word3");
			var dictValue3 = stack.Get("Dict[%KeyInDict%]");
			Assert.AreEqual("Nr3", dictValue3);
		}

		[TestMethod]
		public void GetTest()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			var list = new List<DataTestClass>();
			list.Add(new DataTestClass() { Number = 1, Title = "Hello", Date = DateTime.Now });
			list.Add(new DataTestClass() { Number = 2, Title = "Hello2", Date = DateTime.Now.AddDays(1) });
			stack.Put("dataTestList", list);

			var obj = stack.Get("dataTestList[1].Title.ToUpper()");
			Assert.AreEqual("HELLO", obj);

		}

		[TestMethod]
		public void GetTestListWithIndex()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			var list = new List<DataTestClass>();
			list.Add(new DataTestClass() { Number = 1, Title = "Hello", Date = DateTime.Now });
			list.Add(new DataTestClass() { Number = 2, Title = "Hello2", Date = DateTime.Now.AddDays(1) });
			stack.Put("dataTestList", list);

			var obj = stack.Get("dataTestList.1.Title.ToUpper()");
			Assert.AreEqual("HELLO", obj);

		}

		[TestMethod]
		public void GetTest_GetJson()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			var list = new List<DataTestClass>();
			list.Add(new DataTestClass() { Number = 1, Title = "Hello", Date = new DateTime(2023, 9, 28) });
			list.Add(new DataTestClass() { Number = 2, Title = "Hello2", Date = new DateTime(2023, 9, 28) });
			stack.Put("dataTestList", list);

			var obj = stack.Get("dataTestList.ToJson()");
			Assert.AreEqual("[{\"Number\":1,\"Title\":\"Hello\",\"Date\":\"2023-09-28T00:00:00\"},{\"Number\":2,\"Title\":\"Hello2\",\"Date\":\"2023-09-28T00:00:00\"}]", obj);

		}

		public record JsonTester(string name, int number);
		[TestMethod]
		public void GetTest_GetJson_Record()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			var item = new JsonTester("Hello", 1);
			stack.Put("item", item);
			var obj = stack.Get("item.ToJson()");
			Assert.AreEqual("{\"name\":\"Hello\",\"number\":1}", obj);

		}

		[TestMethod]
		public void GetTest_GetJson_OnJsonContent()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			var item = @"{""name"":""Hello2"",""number"":2}";
			stack.Put("item", item);
			var obj = stack.Get("item.ToJson()");
			Assert.AreEqual("{\r\n  \"name\": \"Hello2\",\r\n  \"number\": 2\r\n}", obj);

		}

		[TestMethod]
		public void TestNow()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			var dateTime = stack.Get("Now");
			Assert.IsNotNull(dateTime);
		}

		[TestMethod]
		public void TestNow_Add()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			var dateTime = (DateTime)stack.Get("Now+1day");
			Assert.AreEqual(DateTime.Now.AddDays(1).ToShortDateString(), dateTime.ToShortDateString());

			dateTime = (DateTime)stack.Get("Now+1hour");
			Assert.AreEqual(DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm"), dateTime.ToString("yyyy-MM-dd HH:mm"));

			dateTime = (DateTime)stack.Get("Now+1 min");
			Assert.AreEqual(DateTime.Now.AddMinutes(1).ToString("yyyy-MM-dd HH:mm"), dateTime.ToString("yyyy-MM-dd HH:mm"));

			dateTime = (DateTime)stack.Get("Now+1year");
			Assert.AreEqual(DateTime.Now.AddYears(1).ToString("yyyy-MM-dd"), dateTime.ToString("yyyy-MM-dd"));

			dateTime = (DateTime)stack.Get("Now-1day");
			Assert.AreEqual(DateTime.Now.AddDays(-1).ToShortDateString(), dateTime.ToShortDateString());

			dateTime = (DateTime)stack.Get("Now-1hour");
			Assert.AreEqual(DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm"), dateTime.ToString("yyyy-MM-dd HH:mm"));

			dateTime = (DateTime)stack.Get("Now-1 min");
			Assert.AreEqual(DateTime.Now.AddMinutes(-1).ToString("yyyy-MM-dd HH:mm"), dateTime.ToString("yyyy-MM-dd HH:mm"));

			dateTime = (DateTime)stack.Get("Now-1year");
			Assert.AreEqual(DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd"), dateTime.ToString("yyyy-MM-dd"));

			string strDate = (string)stack.Get("Now.ToString(\"yyyy-MM-dd\")");
			Assert.AreEqual(DateTime.Now.ToString("yyyy-MM-dd"), strDate);

		}

		[TestMethod]
		public void TestRemove()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.Put("item", 1);
			var item = stack.Get("item");
			Assert.IsNotNull(item);
			stack.Remove("item");
			item = stack.Get("item");
			Assert.IsNull(item);

		}

		[TestMethod]
		public void TestOnCreateVariable()
		{
			var context = new PLangAppContext();
			context.Add(ReservedKeywords.Goal, new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() });
			engine.GetContext().Returns(context);
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.AddOnCreateEvent("item", "Test", new());

			stack.Put("item", 1);

			pseudoRuntime.Received(1).RunGoal(Arg.Any<IEngine>(), Arg.Any<PLangAppContext>(), Path.DirectorySeparatorChar.ToString(), "Test", Arg.Any<Dictionary<string, object>>());
		}

		[TestMethod]
		public void TestOnChangeVariable()
		{
			var context = new PLangAppContext();
			context.Add(ReservedKeywords.Goal, new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() });
			engine.GetContext().Returns(context);
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.AddOnChangeEvent("item", "Test", new());

			//first add the item, no event called
			stack.Put("item", 1);

			//change the item, now the event is called
			stack.Put("item", 2);

			pseudoRuntime.Received(1).RunGoal(Arg.Any<IEngine>(), Arg.Any<PLangAppContext>(), Path.DirectorySeparatorChar.ToString(), "Test", Arg.Any<Dictionary<string, object>>());
		}


		[TestMethod]
		public void TestOnRemoveVariable()
		{
			var context = new PLangAppContext();
			context.Add(ReservedKeywords.Goal, new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() });
			engine.GetContext().Returns(context);
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.AddOnRemoveEvent("item", "Test", new());

			//first add the item, no event called
			stack.Put("item", 1);

			//change the item, now the event is called
			stack.Remove("item");

			pseudoRuntime.Received(1).RunGoal(Arg.Any<IEngine>(), Arg.Any<PLangAppContext>(), Path.DirectorySeparatorChar.ToString(), "Test", Arg.Any<Dictionary<string, object>>());
		}


		[TestMethod]
		public void TestOnRemoveStaticVariable()
		{
			var context = new PLangAppContext();
			context.Add(ReservedKeywords.Goal, new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() });
			engine.GetContext().Returns(context);

			FieldInfo fieldInfo = typeof(MemoryStack).GetField("staticVariables", BindingFlags.Static | BindingFlags.NonPublic);
			fieldInfo.SetValue(null, new Dictionary<string, ObjectValue>());

			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.AddOnRemoveEvent("item", "Test", true, new());

			//first add the item, no event called
			stack.PutStatic("item", 1);

			//change the item, now the event is called
			stack.RemoveStatic("item");

			pseudoRuntime.Received(1).RunGoal(Arg.Any<IEngine>(), Arg.Any<PLangAppContext>(), Path.DirectorySeparatorChar.ToString(), "Test", Arg.Any<Dictionary<string, object>>());
		}


		[TestMethod]
		public void TestOnCreateStaticVariable()
		{
			FieldInfo fieldInfo = typeof(MemoryStack).GetField("staticVariables", BindingFlags.Static | BindingFlags.NonPublic);
			fieldInfo.SetValue(null, new Dictionary<string, ObjectValue>());

			var context = new PLangAppContext();
			context.Add(ReservedKeywords.Goal, new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() });
			engine.GetContext().Returns(context);
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			
			stack.AddOnCreateEvent("item", "Test", true, new());

			//first add the item, no event called
			stack.PutStatic("item", 1);

			//lets modify it, it should not call the goal
			stack.PutStatic("item", 2);

			pseudoRuntime.Received(1).RunGoal(Arg.Any<IEngine>(), Arg.Any<PLangAppContext>(), Path.DirectorySeparatorChar.ToString(), "Test", Arg.Any<Dictionary<string, object>>());
		}
		[TestMethod]
		public void TestOnChangeStaticVariable()
		{
			FieldInfo fieldInfo = typeof(MemoryStack).GetField("staticVariables", BindingFlags.Static | BindingFlags.NonPublic);
			fieldInfo.SetValue(null, new Dictionary<string, ObjectValue>());


			var context = new PLangAppContext();
			context.Add(ReservedKeywords.Goal, new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() });
			engine.GetContext().Returns(context);
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.AddOnChangeEvent("item", "Test", true, new());

			//first add the item, no event called
			stack.PutStatic("item", 1);

			// now change the variable
			stack.PutStatic("item", 2);

			pseudoRuntime.Received(1).RunGoal(Arg.Any<IEngine>(), Arg.Any<PLangAppContext>(), Path.DirectorySeparatorChar.ToString(), "Test", Arg.Any<Dictionary<string, object>>());
		}

		[TestMethod]
		public void PutTest()
		{
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.Put("name", "John");
			stack.Put("age", 29);
			stack.Put("weight", 80.5);
			stack.Put("data", new { foo = 1, bar = 2 });

			var nameObj = stack.Get("name");
			Assert.AreEqual("John", nameObj);
			Assert.AreEqual(typeof(string), nameObj.GetType());

			var ageObj = stack.Get("age");
			Assert.AreEqual(29, ageObj);
			Assert.AreEqual(typeof(int), ageObj.GetType());

			var weightObj = stack.Get("weight");
			Assert.AreEqual(80.5, weightObj);
			Assert.AreEqual(typeof(double), weightObj.GetType());


			var dataObj = (dynamic) stack.Get("data");
			Assert.AreEqual(1, dataObj.foo);
			Assert.AreEqual(2, dataObj.bar);

			stack.Put("data.foo", 3);
			dataObj = (dynamic)stack.Get("data");
			Assert.AreEqual(3, dataObj.foo);

			var testClass = new DataTestClass();
			testClass.Number = 100;
			testClass.Title = "Hello world";
			stack.Put("testClass", testClass);

			var mTestClass = stack.Get("testClass") as DataTestClass;
			Assert.AreEqual(testClass.Number, mTestClass.Number);
			Assert.AreEqual(testClass.GetType(), mTestClass.GetType());

			stack.Put("testClass.Number", "200");
			var mTestClass2 = stack.Get("testClass") as DataTestClass;
			Assert.AreEqual(200, mTestClass2.Number);

			var title = stack.Get("testClass.Title.ToUpper()");
			Assert.AreEqual(testClass.Title.ToUpper(), title);

			string json = @"{
	""id"": ""FinnairVirginAtlantic7:55JFK6k40mBeintflug19:35LHR18:35LHR7k35mBeintflug21:10JFK"",
	""airlines"": ""Finnair Virgin Atlantic"",
	""origin"": ""JFK"",
	""destination"": ""LHR"",
	""price"": ""6k"",
	""start_date"": ""7:55"",
	""end_date"": ""21:10""
}";
			stack.Put("data", json);

			var jsonObj = stack.Get("data");
			Assert.AreEqual(typeof(JObject), jsonObj.GetType());

			string jsonArray = @"[{
	""id"": ""FinnairVirginAtlantic7:55JFK6k40mBeintflug19:35LHR18:35LHR7k35mBeintflug21:10JFK"",
	""airlines"": ""Finnair Virgin Atlantic"",
	""origin"": ""JFK"",
	""destination"": ""LHR"",
	""price"": ""6k"",
	""start_date"": ""7:55"",
	""end_date"": ""21:10""
}, {
	""id"": ""FinnairVirginAtlantic7:55JFK6k40mBeintflug19:35LHR18:35LHR7k35mBeintflug21:10JFK"",
	""airlines"": ""Finnair Virgin Atlantic"",
	""origin"": ""JFK"",
	""destination"": ""LHR"",
	""price"": ""6k"",
	""start_date"": ""7:55"",
	""end_date"": ""21:10""
}]";

			stack.Put("jsonArray", jsonArray);

			var jsonArrayObj = stack.Get("jsonArray");
			Assert.AreEqual(typeof(JArray), jsonArrayObj.GetType());

			var airlines = stack.Get("jsonArray.airlines") as IEnumerable;
			Assert.AreEqual(2, airlines.Cast<object>().Count());
		}
		/*
		[TestMethod()]
		public void PutTest()
		{
			
			var stack = new MemoryStack(pseudoRuntime, engine, context);
			stack.Put("name", "John");
			stack.Put("age", 29);
			stack.Put("weight", 80.5);
			stack.Put("data", new {foo=1, bar=2});

			var variables = stack.GetMemoryStack();
			Assert.AreEqual(4, variables.Count);

			Assert.AreEqual("John", stack.Get("name"));

			Assert.AreEqual(29, stack.Get("age"));
			Assert.AreEqual(80.5, stack.Get("weight"));
			Assert.AreEqual(1, stack.Get<int>("data.foo"));

			stack.Put("user", new { id = 1, name = "John" });
			var obj = stack.Get<dynamic>("user");
			Assert.AreEqual(obj.id, 1);

			stack.Put("user.id", 2);
			obj = stack.Get<dynamic>("user");
			Assert.AreEqual(obj.id, 2);

			string json = $@"{{
  ""id"": 123456,
  ""name"": ""John Doe"",
  ""email"": ""john.doe@example.com"",
  ""isVerified"": true,
  ""profile"": {{
    ""age"": 32,
    ""gender"": ""Male"",
    ""address"": {{
      ""street"": ""123 Main St"",
      ""city"": ""Springfield"",
      ""state"": ""IL"",
      ""zip"": ""62701""
    }},
    ""interests"": [""Reading"", ""Traveling"", ""Cooking""]
  }},
  ""accountCreationDate"": ""2021-08-08T14:30:00Z"",
  ""lastLogin"": ""2023-08-08T10:20:00Z""
}}
";
			stack.Put("userInfo", json);
			var city = stack.Get("userInfo.profile.address.city");
			Assert.AreEqual("Springfield", city);


			var products = $@"[
  {{
    ""productId"": 101,
    ""productName"": ""Laptop"",
    ""price"": 999.99,
    ""inStock"": true
  }},
  {{
    ""productId"": 102,
    ""productName"": ""Smartphone"",
    ""price"": 499.99,
    ""inStock"": false
  }},
  {{
    ""productId"": 103,
    ""productName"": ""Tablet"",
    ""price"": 299.99,
    ""inStock"": true
  }}
]
";
			stack.Put("products", products);
			var list = stack.Get<List<dynamic>>("products");
			Assert.AreEqual(3, list.Count);

			var userInfoList = stack.Get<List<dynamic>>("userInfo");
			Assert.AreEqual(1, userInfoList.Count);

			List<Item> items = new List<Item>();
			items.Add(new Item(100, "Hello"));
			items.Add(new Item(200, "World"));
			stack.Put("ItemsList", items);
			var listItems = stack.Get("ItemsList");
			Assert.AreEqual(2, ((List<Item>)listItems).Count);

			var id = stack.Get<int>("ItemsList[1].Id");
			Assert.AreEqual(200, id);

		}
		*/
		public record Item(int Id, string Name);
	}
}