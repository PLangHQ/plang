using Microsoft.VisualStudio.TestTools.UnitTesting;
using Namotion.Reflection;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.X509;
using PLang.Models.ObjectTypes;
using PLangTests;
using System.Xml.Linq;
using static PLang.Runtime.Tests.ObjectValueTests;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace PLang.Runtime.Tests
{
	[TestClass]
	public class ObjectValueTests
	{
		[TestInitialize]
		public void Init()
		{
		}

		public class TestUser(string name, int age, DateTime created, long longNumber, decimal decimalNumber, TestAddress address, List<int> numbers)
		{
			public string Name { get; } = name;
			public int Age { get; } = age;
			public DateTime Created { get; } = created;
			public long LongNumber { get; } = longNumber;
			public decimal DecimalNumber { get; } = decimalNumber;
			public TestAddress Address { get; } = address;
			public List<int> Numbers { get; } = numbers;
		}

		public class TestAddress(string street, int zip)
		{
			public string Street { get; } = street;
			public int Zip { get; } = zip;
		}

		[TestMethod]
		public void Test_ClassObjectValue()
		{
			string expectedName = "Micheal Scott";
			int expectedAge = 43;
			DateTime expectedCreated = DateTime.Now;
			long expectedLong = long.MaxValue;
			decimal expectedDecimal = decimal.MaxValue;
			string expectedStreet = "MainStreet 23";
			int expectedZip = 122;
			List<int> expectedNumbers = [100, 200, 300];
			var testUser = new TestUser(expectedName, expectedAge, expectedCreated, expectedLong, expectedDecimal, new TestAddress(expectedStreet, expectedZip), expectedNumbers);
			ObjectValue ov = new ObjectValue("user", testUser);

			var name = ov.Get<string>("name");
			Assert.AreEqual(expectedName, name);

			var age = ov.Get<int>("AGE");
			Assert.AreEqual(expectedAge, age);

			var created = ov.Get<DateTime>("created");
			Assert.AreEqual(expectedCreated, created);

			var longNumber = ov.Get<long>("LongNumber");
			Assert.AreEqual(expectedLong, longNumber);
			var decimalNumber = ov.Get<decimal>("decimalNumber");
			Assert.AreEqual(expectedDecimal, decimalNumber);


			var street = ov.Get<string>("Address.street");
			Assert.AreEqual(expectedStreet, street);

			var zip = ov.Get<int>("address.zip");
			Assert.AreEqual(expectedZip, zip);


			var numbers = ov.Get<List<int>>("numbers");
			Assert.AreEqual(expectedNumbers.Count, numbers.Count);

			var number300 = ov.Get<int>("numbers[2]");
			Assert.AreEqual(300, number300);

			var numbersSum = ov.Get<int>("numbers.sum");
			Assert.AreEqual(600, numbersSum);

			var memoryStack = new MemoryStack(null, null, null, null, null);
			memoryStack.Put(new ObjectValue("index", 1));

			var number200 = ov.Get<int>("numbers[index]", memoryStack);
			Assert.AreEqual(200, number200);


			string expectedReplaceName = "Micheal Ben";
			var changedName = ov.Get<string>("name.Replace(\"Scott\", \"Ben\")", memoryStack);

			Assert.AreEqual(expectedReplaceName, changedName);

			int expectedCount = 3;
			var count = ov.Get<int>("numbers.count", memoryStack);

			Assert.AreEqual(expectedCount, count);

			memoryStack.Put("user", testUser);
			var name2 = memoryStack.GetObjectValue("user.name");

			Assert.AreEqual(expectedName, name2.Value);


		}


		[TestMethod]
		public void Test_JObjectObjectValue()
		{

			var jobject = JObject.Parse(@"{
  ""name"": ""John Doe"",
  ""age"": 32,
  ""created"": ""2025-05-26T14:30:00Z"",
  ""longNumber"": 9876543210,
  ""decimalNumber"": 1234.56,
  ""address"": {
    ""street"": ""123 Main St"",
    ""zip"": 90210
  },
  ""numbers"": [1, 5, 9, 15, 42]
}");
			ObjectValue ov = new ObjectValue("user", jobject);

			var name = ov.Get<string>("name");
			Assert.AreEqual("John Doe", name);

			var age = ov.Get<int>("AGE");
			Assert.AreEqual(32, age);

			var created = ov.Get<DateTime>("created");
			Assert.AreEqual(DateTime.Parse("2025-05-26T14:30:00Z"), created);

			var longNumber = ov.Get<long>("LongNumber");
			Assert.AreEqual(9876543210, longNumber);
			var decimalNumber = ov.Get<decimal>("decimalNumber");
			Assert.AreEqual(1234.56M, decimalNumber);


			var street = ov.Get<string>("Address.street");
			Assert.AreEqual("123 Main St", street);

			var zip = ov.Get<int>("address.zip");
			Assert.AreEqual(90210, zip);


			var numbers = ov.Get<List<int>>("numbers");
			Assert.AreEqual(5, numbers.Count);

			var number9 = ov.Get<int>("numbers[2]");
			Assert.AreEqual(9, number9);

			var memoryStack = new MemoryStack(null, null, null, null, null);
			memoryStack.Put(new ObjectValue("index", 1));

			var number5 = ov.Get<int>("numbers[index]", memoryStack);
			Assert.AreEqual(5, number5);

			string expectedReplaceName = "John Ben";
			var changedName = ov.Get<string>("name.Replace(\"Doe\", \"Ben\")", memoryStack);

			Assert.AreEqual(expectedReplaceName, changedName);

			int expectedCount = 5;
			var count = ov.Get<int>("numbers.count", memoryStack);

			Assert.AreEqual(expectedCount, count);

		}

		[TestMethod]
		public void Test_JArrayObjectValue()
		{

			var jArray = JArray.Parse(@"[{
  ""name"": ""John Doe"",
  ""age"": 32,
  ""created"": ""2025-05-26T14:30:00Z"",
  ""longNumber"": 9876543210,
  ""decimalNumber"": 1234.56,
  ""address"": {
    ""street"": ""123 Main St"",
    ""zip"": 90210
  },
  ""numbers"": [1, 5, 9, 15, 42]
}, {
  ""name"": ""John Doe 2"",
  ""age"": 33,
  ""created"": ""2025-05-26T14:30:00Z"",
  ""longNumber"": 9876543211,
  ""decimalNumber"": 1234.57,
  ""address"": {
    ""street"": ""1234 Main St"",
    ""zip"": 902102
  },
  ""numbers"": [2, 6, 10]
}]");
			ObjectValue ov = new ObjectValue("users", jArray);

			var name = ov.Get<string>("name");
			Assert.AreEqual("John Doe", name);

			var age = ov.Get<int>("AGE");
			Assert.AreEqual(32, age);

			var created = ov.Get<DateTime>("created");
			Assert.AreEqual(DateTime.Parse("2025-05-26T14:30:00Z"), created);

			var longNumber = ov.Get<long>("LongNumber");
			Assert.AreEqual(9876543210, longNumber);
			var decimalNumber = ov.Get<decimal>("decimalNumber");
			Assert.AreEqual(1234.56M, decimalNumber);


			var street = ov.Get<string>("Address.street");
			Assert.AreEqual("123 Main St", street);

			var streets = ov.Get<List<string>>("Address.street");
			Assert.AreEqual("123 Main St", streets[0]);
			Assert.AreEqual("1234 Main St", streets[1]);

			var zip = ov.Get<int>("address.zip");
			Assert.AreEqual(90210, zip);


			var numbers = ov.Get<List<int>>("numbers[0]");
			Assert.AreEqual(5, numbers.Count);

			var number3 = ov.Get<List<int>>("numbers[1]");
			Assert.AreEqual(3, number3.Count);

			var memoryStack = new MemoryStack(null, null, null, null, null);
			memoryStack.Put(new ObjectValue("index", 1));

			var number5 = ov.Get<List<int>>("numbers[index]", memoryStack);
			Assert.AreEqual(3, number5.Count);

			var name1 = ov.Get<string>("name[1]");
			Assert.AreEqual("John Doe 2", name1);

			var age1 = ov.Get<int>("AGE[1]");
			Assert.AreEqual(33, age1);

			string expectedReplaceName = "John Ben";
			string expectedReplaceName2 = "John Ben 2";
			var changedNames = ov.Get<List<string>>("name.Replace(\"Doe\", \"Ben\")", memoryStack);

			Assert.AreEqual(expectedReplaceName, changedNames[0]);
			Assert.AreEqual(expectedReplaceName2, changedNames[1]);

			int expectedCount = 3;
			var counts = ov.Get<List<int>>("numbers.count", memoryStack);

			Assert.AreEqual(5, counts[0]);
			Assert.AreEqual(3, counts[1]);


		}

		[TestMethod]
		public void Test_AnonObjectValue()
		{
			string expectedName = "Micheal Scott";
			int expectedAge = 43;
			DateTime expectedCreated = DateTime.Now;
			long expectedLong = long.MaxValue;
			decimal expectedDecimal = decimal.MaxValue;
			string expectedStreet = "MainStreet 23";
			int expectedZip = 122;
			List<int> expectedNumbers = [100, 200, 300];

			var user = new
			{
				name = expectedName,
				age = expectedAge,
				created = expectedCreated,
				longNumber = expectedLong,
				decimalNumber = expectedDecimal,
				address = new
				{
					street = expectedStreet,
					zip = expectedZip
				},
				numbers = expectedNumbers
			};



			ObjectValue ov = new ObjectValue("user", user);

			var name = ov.Get<string>("name");
			Assert.AreEqual(expectedName, name);

			var age = ov.Get<int>("AGE");
			Assert.AreEqual(expectedAge, age);

			var created = ov.Get<DateTime>("created");
			Assert.AreEqual(expectedCreated, created);

			var longNumber = ov.Get<long>("LongNumber");
			Assert.AreEqual(expectedLong, longNumber);
			var decimalNumber = ov.Get<decimal>("decimalNumber");
			Assert.AreEqual(expectedDecimal, decimalNumber);


			var street = ov.Get<string>("Address.street");
			Assert.AreEqual(expectedStreet, street);

			var zip = ov.Get<int>("address.zip");
			Assert.AreEqual(expectedZip, zip);


			var numbers = ov.Get<List<int>>("numbers");
			Assert.AreEqual(expectedNumbers.Count, numbers.Count);

			var number300 = ov.Get<int>("numbers[2]");
			Assert.AreEqual(300, number300);

			var memoryStack = new MemoryStack(null, null, null, null, null);
			memoryStack.Put(new ObjectValue("index", 1));

			var number200 = ov.Get<int>("numbers[index]", memoryStack);
			Assert.AreEqual(200, number200);
		}



		[TestMethod]
		public void Test_HtmlObjectValue()
		{

			string html = """<html><head><title>Hello</title></head><body><table id="tbl"><tr><td>Title</td></tr><tr><td>The office</td></tr></table>""";

			var objectValue = new ObjectValue("html", new HtmlType(html));

			string title = objectValue.Get<string>("html.head.title");
			Assert.AreEqual("Hello", title);

			string tbl = objectValue.Get<string>("html.body.table#tbl");
			Assert.AreEqual("""<tr><td>Title</td></tr><tr><td>The office</td></tr>""", tbl);


			string title2 = objectValue.Get<string>("title");
			Assert.AreEqual("Hello", title2);


			string html2 = """<html><head><title>Hello</title></head><body><table id="tbl"><tr><td>Title</td></tr><tr><td>The office</td></tr></table><table><tr><td>Park and Rec</td></tr></table>""";

			var objectValue2 = new ObjectValue("html", new HtmlType(html2));
			var tables = objectValue2.Get<List<string>>("table");
			Assert.AreEqual(2, tables.Count);

		}


		[TestMethod]
		public void Test_ObjectValue_TestMath()
		{
			var order = new
			{
				items = new[]
					{
						new { name = "Product 1", price = 100 },
						new { name = "Product 2", price = 150 }
					}
			};

			var objectValue = new ObjectValue("order", order);
			var sum = objectValue.Get<int>("items.price.sum");

			Assert.AreEqual(250, sum);



			var sumDiv = objectValue.Get<int>("items.price.sum * 0,8");
			Assert.AreEqual(200, sumDiv);

			var sumDiv2 = objectValue.Get<double>("items.price.sum * 0,77");
			Assert.AreEqual(192.5, sumDiv2);
			var sumDiv3 = objectValue.Get<double>("items.price.sum * 0,77.round(0)");
			Assert.AreEqual(192, sumDiv3);
		}

		[TestMethod]
		public void Test_ObjectValue_TestDate()
		{
			var expected = DateTimeOffset.Now;

			var objectValue = new DynamicObjectValue("now", () => { return expected; });
			var dt = objectValue.Get<DateTimeOffset>("+5days");

			Assert.AreEqual(expected.AddDays(5), dt);


			var memoryStack = new MemoryStack(null, null, null, null, null);
			memoryStack.Put(new DynamicObjectValue("now", () => { return expected; }));

			var ov = memoryStack.GetObjectValue("%now%");
			Assert.AreEqual(expected, ov.Value);

			var ov2 = memoryStack.GetObjectValue("%now+5days%");
			Assert.AreEqual(expected.AddDays(5), ov2.Value);

		}


		[TestMethod]
		public void Test_ObjectValue_TestSet()
		{
			var user = new
			{
				name = "abc",
				zip = 100
			};

			var objectValue = new ObjectValue("user", user);
			var zipObjectValue = new ObjectValue("zip", 200, parent: objectValue);

			objectValue.Set(zipObjectValue.PathAsVariable, zipObjectValue);


			var zip = objectValue.Get<int>("zip");

			Assert.AreEqual(200, zip);
			int i = 0;
		}
	}
}