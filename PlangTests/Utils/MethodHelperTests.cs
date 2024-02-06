using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Utils;
using PLangTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.BaseBuilder;
using static PLang.Utils.MethodHelper;

namespace PLang.Utils.Tests
{
	[TestClass()]
	public class MethodHelperTests : BasePLangTest
	{
        MethodHelper methodHelper;
        GoalStep goalStep;
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
			goalStep = new GoalStep();
            goalStep.Text = "";

            var typeHelper = new TypeHelper(fileSystem, settings);
			methodHelper = new MethodHelper(goalStep, variableHelper, memoryStack, typeHelper, aiService);

		}
	
	
		public void AddToList(object value, List<object> listInstance) { }
		[TestMethod()]
		public async Task GetMethodAndParameters_List_WithJsonContentToAdd_Test()
		{
			var products = new List<object>();
			products.Add(new { Name = "Product1", Price = 12 });
			products.Add(new { Name = "Product2", Price = 22 });
			memoryStack.Put("products", products);

			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""AddToList"",
    ""Parameters"": [
      {
        ""Type"": ""Object"",
        ""Name"": ""value"",
        ""Value"": {
          ""name"": ""Product3"",
          ""price"": 333
        }
      },
      {
        ""Type"": ""List`1"",
        ""Name"": ""listInstance"",
        ""Value"": ""%products%""
      }
    ],
    ""ReturnValue"": null
  }");


		
			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("AddToList", result.method.Name);
			Assert.AreEqual(2, result.method.GetParameters().Length);
			Assert.IsTrue(result.parameterValues["value"].ToString().Contains("Product3"));

			var list = result.parameterValues["listInstance"] as List<object>;
			Assert.AreEqual(2, list.Count);
		}
		public async Task<string> ReadTextFile(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false) { return returnValueIfFileNotExisting; }


		[TestMethod()]
		public async Task GetMethodAndParameters_primatives_Test()
		{
			memoryStack.Put("fileName", "file.txt");

	
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""ReadTextFile"",
    ""Parameters"": [
      {
        ""Type"": ""String"",
        ""Name"": ""path"",
        ""Value"": ""%fileName%""
      },
      {
        ""Type"": ""String"",
        ""Name"": ""returnValueIfFileNotExisting"",
        ""Value"": """"
      },
      {
        ""Type"": ""Boolean"",
        ""Name"": ""throwErrorOnNotFound"",
        ""Value"": false
      }
    ],
    ""ReturnValue"": [{
      ""Type"": ""String"",
      ""VariableName"": ""content""
    }]
  }");


			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("ReadTextFile", result.method.Name);
			Assert.AreEqual(3, result.method.GetParameters().Length);
			Assert.AreEqual("file.txt", result.parameterValues["path"]);
			Assert.AreEqual("", result.parameterValues["returnValueIfFileNotExisting"]);
			Assert.AreEqual(false, result.parameterValues["throwErrorOnNotFound"]);

		}
		public async Task WriteExcelFile(string path, string[] variablesToWriteToExcel, int excelType = 0, char seperator = ',', bool printHeader = true, bool overwrite = false) { }

		[TestMethod()]
		public async Task GetMethodAndParameters_array_Test()
		{
			memoryStack.Put("csvData", "1,2,3");

			var goalStep = new Building.Model.GoalStep();
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""WriteExcelFile"",
    ""Parameters"": [
      {
        ""Type"": ""String"",
        ""Name"": ""path"",
        ""Value"": ""demo.xlsx""
      },
      {
        ""Type"": ""String[]"",
        ""Name"": ""variablesToWriteToExcel"",
        ""Value"": ""%csvData%""
      },
      {
        ""Type"": ""Boolean"",
        ""Name"": ""overwrite"",
        ""Value"": true
      }
    ],
    ""ReturnValue"": null
  }");


			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("WriteExcelFile", result.method.Name);
			Assert.AreEqual(1, ((string[])result.parameterValues["variablesToWriteToExcel"]).Length);

		}


		[TestMethod()]
		public async Task GetMethodAndParameters_array2_Test()
		{
			memoryStack.Put("csvData", "1,2,3");
			memoryStack.Put("excelData", "a,b,c");

			var goalStep = new Building.Model.GoalStep();
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""WriteExcelFile"",
    ""Parameters"": [
      {
        ""Type"": ""String"",
        ""Name"": ""path"",
        ""Value"": ""demo.xlsx""
      },
      {
        ""Type"": ""String[]"",
        ""Name"": ""variablesToWriteToExcel"",
        ""Value"": [
          ""%excelData%"",
          ""%csvData%""
        ]
      },
      {
        ""Type"": ""Boolean"",
        ""Name"": ""overwrite"",
        ""Value"": true
      }
    ],
    ""ReturnValue"": null
  }");


			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("WriteExcelFile", result.method.Name);
			Assert.AreEqual(2, ((string[])result.parameterValues["variablesToWriteToExcel"]).Length);

		}

		public async Task WriteExcelFile2(string path, [HandlesVariableAttribute] string[] variablesToWriteToExcel, int excelType = 0, char seperator = ',', bool printHeader = true, bool overwrite = false) { }

		[TestMethod()]
		public async Task GetMethodAndParameters_array3_HandleVariable_Test()
		{
			memoryStack.Put("csvData", "1,2,3");
			memoryStack.Put("excelData", "a,b,c");

			var goalStep = new Building.Model.GoalStep();
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""WriteExcelFile2"",
    ""Parameters"": [
      {
        ""Type"": ""String"",
        ""Name"": ""path"",
        ""Value"": ""demo.xlsx""
      },
      {
        ""Type"": ""String[]"",
        ""Name"": ""variablesToWriteToExcel"",
        ""Value"": [
          ""%excelData%"",
          ""%csvData%""
        ]
      },
      {
        ""Type"": ""Boolean"",
        ""Name"": ""overwrite"",
        ""Value"": true
      }
    ],
    ""ReturnValue"": null
  }");


			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("WriteExcelFile2", result.method.Name);
			Assert.AreEqual(2, ((string[])result.parameterValues["variablesToWriteToExcel"]).Length);

		}


        public async Task<string> HashInput(string input, int? ble, bool useSalt = true, string? salt = null, string hashAlgorithm = "keccak256") { return ""; }
		[TestMethod()]
		public async Task GetMethodAndParameters_Nullable_Test()
		{
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""HashInput"",
    ""Parameters"": [
      {
        ""Type"": ""String"",
        ""Name"": ""input"",
        ""Value"": ""%password%""
      }
    ],
    ""ReturnValue"": [{
      ""Type"": ""String"",
      ""VariableName"": ""hashedPassword""
    }]
  }");
            memoryStack.Put("password", "123");
            goalStep.Text = "hash %password%, write to %hashedPassword%";
			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("HashInput", result.method.Name);
			Assert.AreEqual("123", result.parameterValues["input"]);
			Assert.AreEqual(null, result.parameterValues["ble"]);
			Assert.AreEqual(true, result.parameterValues["useSalt"]);
			Assert.AreEqual(null, result.parameterValues["salt"]);
			Assert.AreEqual("keccak256", result.parameterValues["hashAlgorithm"]);

		}


		public async Task<string> DoStuff(int d) { return ""; }
		[TestMethod()]
        [ExpectedException(typeof(MissingMethodException))]
		public async Task GetMethodAndParameters_ThrowsMethodNotFound_Test()
		{
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""DoStuff"",
    ""Parameters"": [
      {
        ""Type"": ""String"",
        ""Name"": ""a"",
        ""Value"": ""%password%""
      }
    ]
  }");
			memoryStack.Put("password", "123");
			goalStep.Text = "hash %password%, write to %hashedPassword%";
			aiService.Query<MethodNotFoundResponse>(Arg.Any<LlmQuestion>()).Returns(new MethodNotFoundResponse("hash %password%, write into %hashedPassword%"));
			var result = await methodHelper.GetMethodAndParameters(this, gf);


		}



		public async Task<int> Insert(string sql, Dictionary<string, object>? Parameters = null) { return 1; }
		[TestMethod()]
		public async Task GetMethodAndParameters_Dictionary_Test()
		{
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""Insert"",
    ""Parameters"": [
      {
        ""Type"": ""string"",
        ""Name"": ""sql"",
        ""Value"": ""insert into tasks (id, description, due_date) values (@id, @description, @due_date)""
      },
      {
        ""Type"": ""Dictionary`2"",
        ""Name"": ""Parameters"",
        ""Value"": {
          ""@id"": ""%id%"",
          ""@description"": ""%description%"",
          ""@due_date"": ""%due_date%""
        }
      }
    ],
    ""ReturnValue"": [{
      ""Type"": ""Int32"",
      ""VariableName"": ""rows""
    }]
  }");
            var dt = DateTime.Now;
			memoryStack.Put("id", 1);
			memoryStack.Put("description", "desc");
			memoryStack.Put("due_date", dt);

			goalStep.Text = "hash %password%, write to %hashedPassword%";
			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("Insert", result.method.Name);
			Assert.AreEqual("insert into tasks (id, description, due_date) values (@id, @description, @due_date)", result.parameterValues["sql"]);

            var parameters = (Dictionary<string, object>)result.parameterValues["Parameters"];
			Assert.AreEqual(3, parameters.Count);
            Assert.AreEqual(1, parameters["@id"]);
			Assert.AreEqual("desc", parameters["@description"]);
			Assert.AreEqual(dt, parameters["@due_date"]);
		}

		[TestMethod()]
		public async Task GetMethodAndParameters_Dictionary2_Test()
		{
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""Insert"",
    ""Parameters"": [
      {
        ""Type"": ""string"",
        ""Name"": ""sql"",
        ""Value"": ""insert into tasks (id, description, due_date) values (@id, @description, @due_date)""
      },
      {
        ""Type"": ""Dictionary`2"",
        ""Name"": ""Parameters"",
        ""Value"": ""%dict%""
      }
    ],
    ""ReturnValue"": [{
      ""Type"": ""Int32"",
      ""VariableName"": ""rows""
    }]
  }");
			var dt = DateTime.Now;

            var dict = new Dictionary<string, object>();
            dict.Add("id", 1);
			dict.Add("description", "desc");
			dict.Add("due_date", dt);

			memoryStack.Put("dict", dict);

			goalStep.Text = "hash %password%, write to %hashedPassword%";
			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("Insert", result.method.Name);
			Assert.AreEqual("insert into tasks (id, description, due_date) values (@id, @description, @due_date)", result.parameterValues["sql"]);

			var parameters = (Dictionary<string, object>)result.parameterValues["Parameters"];
			Assert.AreEqual(3, parameters.Count);
			Assert.AreEqual(1, parameters["id"]);
			Assert.AreEqual("desc", parameters["description"]);
			Assert.AreEqual(dt, parameters["due_date"]);
		}

		public async Task<int> Insert2(string sql, [HandlesVariable] Dictionary<string, object>? Parameters = null) { return 1; }
		[TestMethod()]
		public async Task GetMethodAndParameters_Dictionary_HandleVariable_Test()
		{
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""Insert2"",
    ""Parameters"": [
      {
        ""Type"": ""string"",
        ""Name"": ""sql"",
        ""Value"": ""insert into tasks (id, description, due_date) values (@id, @description, @due_date)""
      },
      {
        ""Type"": ""Dictionary`2"",
        ""Name"": ""Parameters"",
        ""Value"": {
          ""@id"": ""%id%"",
          ""@description"": ""%description%"",
          ""@due_date"": ""%due_date%""
        }
      }
    ],
    ""ReturnValue"": [{
      ""Type"": ""Int32"",
      ""VariableName"": ""rows""
    }]
  }");
			var dt = DateTime.Now;
			memoryStack.Put("id", 1);
			memoryStack.Put("description", "desc");
			memoryStack.Put("due_date", dt);

			goalStep.Text = "hash %password%, write to %hashedPassword%";
			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("Insert2", result.method.Name);
			Assert.AreEqual("insert into tasks (id, description, due_date) values (@id, @description, @due_date)", result.parameterValues["sql"]);

			var parameters = (Dictionary<string, object>)result.parameterValues["Parameters"];
			Assert.AreEqual(3, parameters.Count);
			Assert.AreEqual("%id%", parameters["@id"]);
			Assert.AreEqual("%description%", parameters["@description"]);
			Assert.AreEqual("%due_date%", parameters["@due_date"]);
		}


		[TestMethod()]
		public async Task GetMethodAndParameters_List_Test()
		{
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""AddToList"",
    ""Parameters"": [
      {
        ""Type"": ""Object"",
        ""Name"": ""value"",
        ""Value"": {
          ""name"": ""Product3"",
          ""price"": 333
        }
      },
      {
        ""Type"": ""List`1"",
        ""Name"": ""listInstance"",
        ""Value"": [{
          ""name"": ""Product1"",
          ""price"": 11
        },{
          ""name"": ""Product2"",
          ""price"": 22
        }]
      }
    ],
    ""ReturnValue"": null
  }");
			var dt = DateTime.Now;
			memoryStack.Put("id", 1);
			memoryStack.Put("description", "desc");
			memoryStack.Put("due_date", dt);

			goalStep.Text = "hash %password%, write to %hashedPassword%";
			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("AddToList", result.method.Name);
			Assert.AreEqual(@"{
  ""name"": ""Product3"",
  ""price"": 333
}", result.parameterValues["value"].ToString());

			Assert.AreEqual(2, result.parameterValues.Count);
		}

		[TestMethod()]
		public async Task GetMethodAndParameters_List_Obj_isNull_Test()
		{
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""AddToList"",
    ""Parameters"": [
      {
        ""Type"": ""Object"",
        ""Name"": ""value"",
        ""Value"": ""%obj%""
      },
      {
        ""Type"": ""List`1"",
        ""Name"": ""listInstance"",
        ""Value"":  ""%list%""
      }
    ],
    ""ReturnValue"": null
  }");
			var list = new List<object>();

			memoryStack.Put("list", list);
			memoryStack.Put("%obj%", null);
			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("AddToList", result.method.Name);
			Assert.AreEqual(null, result.parameterValues["value"]);
			Assert.AreEqual(list, result.parameterValues["listInstance"]);
			Assert.AreEqual(2, result.parameterValues.Count);
		}

		public void AddToList2([HandlesVariable] object value, List<object> listInstance) { }
		[TestMethod()]
		public async Task GetMethodAndParameters_List_HandleVariables_Test()
		{
			var gf = JsonConvert.DeserializeObject<GenericFunction>(@"{
    ""FunctionName"": ""AddToList2"",
    ""Parameters"": [
      {
        ""Type"": ""Object"",
        ""Name"": ""value"",
        ""Value"": ""%obj%""
      },
      {
        ""Type"": ""List`1"",
        ""Name"": ""listInstance"",
        ""Value"":  ""%list%""
      }
    ],
    ""ReturnValue"": null
  }");
            var list = new List<object>();

			memoryStack.Put("list", list);
			var result = await methodHelper.GetMethodAndParameters(this, gf);

			Assert.AreEqual("AddToList2", result.method.Name);
			Assert.AreEqual(@"%obj%", result.parameterValues["value"]);
			Assert.AreEqual(list, result.parameterValues["listInstance"]);
			Assert.AreEqual(2, result.parameterValues.Count);
		}






	}

}