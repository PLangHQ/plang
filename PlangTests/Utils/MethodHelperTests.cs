using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Models;
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
			methodHelper = new MethodHelper(goalStep, variableHelper, memoryStack, typeHelper, llmServiceFactory);

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


		
			var method = await methodHelper.GetMethod(this, gf);
            var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("AddToList", method.Name);
			Assert.AreEqual(2, method.GetParameters().Length);
			Assert.IsTrue(parameters["value"].ToString().Contains("Product3"));

			var list = parameters["listInstance"] as List<object>;
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


			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("ReadTextFile", method.Name);
			Assert.AreEqual(3, method.GetParameters().Length);
			Assert.AreEqual("file.txt", parameters["path"]);
			Assert.AreEqual("", parameters["returnValueIfFileNotExisting"]);
			Assert.AreEqual(false, parameters["throwErrorOnNotFound"]);

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


			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("WriteExcelFile", method.Name);
			Assert.AreEqual("1,2,3", parameters["variablesToWriteToExcel"]);

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


			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("WriteExcelFile", method.Name);
			Assert.AreEqual(2, ((string[])parameters["variablesToWriteToExcel"]).Length);

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


			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("WriteExcelFile2", method.Name);
			Assert.AreEqual(2, ((string[])parameters["variablesToWriteToExcel"]).Length);

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
			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("HashInput", method.Name);
			Assert.AreEqual("123", parameters["input"]);
			Assert.AreEqual(null, parameters["ble"]);
			Assert.AreEqual(true, parameters["useSalt"]);
			Assert.AreEqual(null, parameters["salt"]);
			Assert.AreEqual("keccak256", parameters["hashAlgorithm"]);

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
			llmService.Query<MethodNotFoundResponse>(Arg.Any<LlmRequest>()).Returns((new MethodNotFoundResponse("hash %password%, write into %hashedPassword%"), null));
			var method = await methodHelper.GetMethod(this, gf);


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
			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("Insert", method.Name);
			Assert.AreEqual("insert into tasks (id, description, due_date) values (@id, @description, @due_date)", parameters["sql"]);

			var innerParameters = parameters["Parameters"] as Dictionary<string, object>;
			Assert.AreEqual(3, innerParameters.Count);
            Assert.AreEqual(1, innerParameters["@id"]);
			Assert.AreEqual("desc", innerParameters["@description"]);
			Assert.AreEqual(dt, innerParameters["@due_date"]);
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
			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("Insert", method.Name);
			Assert.AreEqual("insert into tasks (id, description, due_date) values (@id, @description, @due_date)", parameters["sql"]);

			var innerParameters = parameters["Parameters"] as Dictionary<string, object>;
			Assert.AreEqual(3, innerParameters.Count);
			Assert.AreEqual(1, innerParameters["id"]);
			Assert.AreEqual("desc", innerParameters["description"]);
			Assert.AreEqual(dt, innerParameters["due_date"]);
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
			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("Insert2", method.Name);
			Assert.AreEqual("insert into tasks (id, description, due_date) values (@id, @description, @due_date)", parameters["sql"]);

            var innerParameters = parameters["Parameters"] as Dictionary<string, object>;
			Assert.AreEqual(3, innerParameters.Count);
			Assert.AreEqual("%id%", innerParameters["@id"]);
			Assert.AreEqual("%description%", innerParameters["@description"]);
			Assert.AreEqual("%due_date%", innerParameters["@due_date"]);
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
			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("AddToList", method.Name);
			Assert.AreEqual(@"{
  ""name"": ""Product3"",
  ""price"": 333
}", parameters["value"].ToString());

			Assert.AreEqual(2, parameters.Count);
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
			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("AddToList", method.Name);
			Assert.AreEqual(null, parameters["value"]);
			Assert.AreEqual(list, parameters["listInstance"]);
			Assert.AreEqual(2, parameters.Count);
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
			var method = await methodHelper.GetMethod(this, gf);
			var parameters = methodHelper.GetParameterValues(method, gf);
			Assert.AreEqual("AddToList2", method.Name);
			Assert.AreEqual(@"%obj%", parameters["value"]);
			Assert.AreEqual(list, parameters["listInstance"]);
			Assert.AreEqual(2, parameters.Count);
		}






	}

}