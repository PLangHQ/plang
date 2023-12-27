using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Utils;
using PLangTests;
using System.Data;
using System.Data.SQLite;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Modules.DbModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		Builder builder;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();

			settings.Get(typeof(PLangLlmService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new PLangLlmService(cacheHelper, context);

			var datasources =new  List<DataSource>();
			datasources.Add(new DataSource("local", "", "", "", "", ""));
			datasources.Add(new DataSource("MainDb", "", "", "", "", ""));
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(datasources);

			var fileSystem = new PLangFileSystem(Environment.CurrentDirectory, "./");
			typeHelper = new TypeHelper(fileSystem, settings);

			var db = new SQLiteConnection("DataSource=In memeory;Version=3");

			builder = new Builder(fileSystem, db, settings, context, aiService, typeHelper, logger);
			builder.InitBaseBuilder("PLang.Modules.DbModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type, string functionName)
		{
			var db = new SQLiteConnection("DataSource=In memory;Version=3");
			var aiService = Substitute.For<ILlmService>();

			aiService.Query(Arg.Any<LlmQuestion>(), typeof(FunctionInfo)).Returns(p => {
				return JsonConvert.DeserializeObject(@$"{{""FunctionName"": ""{functionName}""}}", typeof(FunctionInfo));
			});

			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 				
				return JsonConvert.DeserializeObject(response, type); 
			});

			var dataSources = new List<DataSource>();
			dataSources.Add(new DataSource("Main", "System.Data.SQLite.SQLiteConnection", "", "", "", ""));
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(dataSources);

			builder = new Builder(fileSystem, db, settings, context, aiService, typeHelper, logger);
			builder.InitBaseBuilder("PLang.Modules.DbModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}

		[DataTestMethod]
		[DataRow("set datasource as 'MainDb'")]
		public async Task SetDataSouceName_Test(string text)
		{
			string response = @"{""FunctionName"": ""SetDataSouceName"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""name"",
""Value"": ""MainDb""}],
""ReturnValue"": null}";


			SetupResponse(response, typeof(GenericFunction), "SetDataSouceName");

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetDataSouceName", gf.FunctionName);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("MainDb", gf.Parameters[0].Value);

		}

		[DataTestMethod]
		[DataRow("begin transaction")]
		public async Task BeginTransaction_Test(string text)
		{

			string response = @"{""tableNames"": [],
""FunctionName"": ""BeginTransaction"",
""Parameters"": [],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction), "BeginTransaction");

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("BeginTransaction", gf.FunctionName);
			Assert.AreEqual(0, gf.Parameters.Count);

		}

		[DataTestMethod]
		[DataRow("end transaction")]
		public async Task EndTransaction_Test(string text)
		{

			string response = @"{""tableNames"": [],
""FunctionName"": ""EndTransaction"",
""Parameters"": [],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction), "EndTransaction");

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("EndTransaction", gf.FunctionName);
			Assert.AreEqual(0, gf.Parameters.Count);

		}



		[DataTestMethod]
		[DataRow("select name from users where id=%id%, write to %result%")]
		public async Task Select_Test(string text)
		{

			string response = @"{""FunctionName"": ""Select"",
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""sql"", ""Value"": ""select name from users where id=@id""},
    {""Type"": ""Dictionary`2"", ""Name"": ""Parameters"", ""Value"": {""@id"":""%id%""}}
],
""ReturnValue"": {""Type"": ""Object"", ""VariableName"": ""result""}}";

			SetupResponse(response, typeof(GenericFunction), "Select");

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Select", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("select name from users where id=@id", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%id%", dict["@id"]);

			Assert.AreEqual("result", gf.ReturnValue.VariableName);

		}



		[DataTestMethod]
		[DataRow("update users, set name=%name% where id=%id%")]
		public async Task Update_Test(string text)
		{

			string response = @"{""TableNames"":[""users""],
""FunctionName"":""Update"",
""Parameters"":[{""Type"":""string"",""Name"":""sql"",""Value"":""update users set name=@name where id=@id""},
		{""Type"":""Dictionary<string, object>"",""Name"":""Parameters"",""Value"":{""@name"":""%name%"",""@id"":""%id%""}}],
""ReturnValue"":null}";

			SetupResponse(response, typeof(DbGenericFunction), "Update");

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as DbGenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Update", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("update users set name=@name where id=@id", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%name%", dict["@name"]);
			Assert.AreEqual("%id%", dict["@id"]);

		}


		[DataTestMethod]
		[DataRow("insert into users (name) values (%name%)")]
		public async Task Insert_Test(string text)
		{

			string response = @"{
  ""FunctionName"": ""Insert"",
  ""Parameters"": [
    {
      ""Type"": ""string"",
      ""Name"": ""sql"",
      ""Value"": ""insert into users (id, name) values (@id, @name)""
    },
    {
      ""Type"": ""Dictionary`2"",
      ""Name"": ""Parameters"",
      ""Value"": {
        ""@id"": ""%id%"",
        ""@name"": ""%name%""
      }
    }
  ],
  ""ReturnValue"": {
    ""Type"": ""Int32"",
    ""VariableName"": null
  }
}";

			SetupResponse(response, typeof(GenericFunction), "Insert");

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Insert", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("insert into users (id, name) values (@id, @name)", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%name%", dict["@name"]);
			Assert.AreEqual("%id%", dict["@id"]);

		}



		[DataTestMethod]
		[DataRow("insert into users (name) values (%name%), write into %id%")]
		public async Task InsertAndSelectId_Test(string text)
		{

			string response = @"{
  ""FunctionName"": ""InsertAndSelectIdOfInsertedRow"",
  ""Parameters"": [
    {
      ""Type"": ""string"",
      ""Name"": ""sql"",
      ""Value"": ""insert into users (id, name) values (@id, @name)""
    },
    {
      ""Type"": ""Dictionary`2"",
      ""Name"": ""Parameters"",
      ""Value"": {
        ""@id"": ""%id%"",
        ""@name"": ""%name%""
      }
    }
  ],
  ""ReturnValue"": {
    ""Type"": ""Object"",
    ""VariableName"": ""id""
  }
}";

			SetupResponse(response, typeof(GenericFunction), "Insert");

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("InsertAndSelectIdOfInsertedRow", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("insert into users (id, name) values (@id, @name)", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%name%", dict["@name"]);
			Assert.AreEqual("%id%", dict["@id"]);

			Assert.AreEqual("id", gf.ReturnValue.VariableName);

		}


		[DataTestMethod]
		[DataRow("delete users where id=%id%")]
		public async Task Delete_Test(string text)
		{

			string response = @"{
""Warning"": null,
""FunctionName"": ""Delete"",
""Parameters"": [
    {
    ""Type"": ""string"",
    ""Name"": ""sql"",
    ""Value"": ""DELETE FROM users WHERE id=@id""
    },
    {
    ""Type"": ""Dictionary`2"",
    ""Name"": ""Parameters"",
    ""Value"": 
        {
        ""@id"": ""%id%""
        }
    }
]
}";

			SetupResponse(response, typeof(DbGenericFunction), "Delete");

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as DbGenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Delete", gf.FunctionName);
			Assert.AreEqual("sql", gf.Parameters[0].Name);
			Assert.AreEqual("DELETE FROM users WHERE id=@id", gf.Parameters[0].Value);

			Assert.AreEqual("Parameters", gf.Parameters[1].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[1].Value.ToString());
			Assert.AreEqual("%id%", dict["@id"]);

		}

	}
}