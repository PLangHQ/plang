using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Utils;
using PLangTests;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.FileModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		BaseBuilder builder;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();

			settings.Get(typeof(PLangLlmService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new PLangLlmService(cacheHelper, context);
			
			var fileSystem = new PLangFileSystem(Environment.CurrentDirectory, "./");
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.FileModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.FileModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}



		[DataTestMethod]
		[DataRow("read %mp4File% to %mp4ContentBase64%")]
		public async Task ReadBinaryFileAndConvertToBase64_Test(string text)
		{
			string response = @"{""FunctionName"": ""ReadBinaryFileAndConvertToBase64"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""path"",
""Value"": ""%mp4File%""}],
""ReturnValue"": {""Type"": ""String"",
""VariableName"": ""mp4ContentBase64""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("ReadBinaryFileAndConvertToBase64", gf.FunctionName);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("%mp4File%", gf.Parameters[0].Value);
			Assert.AreEqual("mp4ContentBase64", gf.ReturnValue.VariableName);

		}


		[DataTestMethod]
		[DataRow("read file.txt to %content%")]
		public async Task ReadTextFile_Test(string text)
		{
			string response = @"{""FunctionName"": ""ReadTextFile"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""path"",
""Value"": ""file.txt""}],
""ReturnValue"": {""Type"": ""String"",
""VariableName"": ""content""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("ReadTextFile", gf.FunctionName);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("file.txt", gf.Parameters[0].Value);
			Assert.AreEqual("content", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("read file.mp4 into %stream%")]
		public async Task ReadFileAsStream_Test(string text)
		{
			string response = @"{""FunctionName"": ""ReadFileAsStream"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""path"",
""Value"": ""file.mp4""}],
""ReturnValue"": {""Type"": ""Stream"",
""VariableName"": ""stream""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("ReadFileAsStream", gf.FunctionName);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("file.mp4", gf.Parameters[0].Value);
			Assert.AreEqual("stream", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("read all files in %dir%, into %contents%", "*", true)]
		[DataRow("read all files in %dir% ending with mp4, dont include sub dirs, into %contents%", "*.mp4", false)]
		public async Task ReadMultipleTextFiles_Test(string text, string pattern, bool includeSubFolders)
		{
			string response = @"{""FunctionName"": ""ReadMultipleTextFiles"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""folderPath"",
""Value"": ""%dir%""},
{""Type"": ""String"",
""Name"": ""filePattern"",
""Value"": """ + pattern + @"""},
{""Type"": ""Boolean"",
""Name"": ""includeAllSubfolders"",
""Value"": " + includeSubFolders.ToString().ToLower() + @"}],
""ReturnValue"": {""Type"": ""List`1"",
""VariableName"": ""%contents%""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("ReadMultipleTextFiles", gf.FunctionName);
			Assert.AreEqual("folderPath", gf.Parameters[0].Name);
			Assert.AreEqual("%dir%", gf.Parameters[0].Value);
			Assert.AreEqual("filePattern", gf.Parameters[1].Name);
			Assert.AreEqual(pattern, gf.Parameters[1].Value);
			Assert.AreEqual("includeAllSubfolders", gf.Parameters[2].Name);
			Assert.AreEqual(includeSubFolders, gf.Parameters[2].Value);
			Assert.AreEqual("%contents%", gf.ReturnValue.VariableName);

		}


		[DataTestMethod]
		[DataRow("write %content% to file.txt, overwrite it")]
		public async Task WriteToFile_Test(string text)
		{
			string response = @"{""FunctionName"": ""WriteToFile"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""path"",
""Value"": ""file.txt""},
{""Type"": ""String"",
""Name"": ""content"",
""Value"": ""%content%""},
{""Type"": ""Boolean"",
""Name"": ""overwrite"",
""Value"": true}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("WriteToFile", gf.FunctionName);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("file.txt", gf.Parameters[0].Value);
			Assert.AreEqual("content", gf.Parameters[1].Name);
			Assert.AreEqual("%content%", gf.Parameters[1].Value);
			Assert.AreEqual("overwrite", gf.Parameters[2].Name);
			Assert.AreEqual(true, gf.Parameters[2].Value);

		}


		[DataTestMethod]
		[DataRow("append %content% to file.txt")]
		public async Task AppendToFile_Test(string text)
		{
			string response = @"{""FunctionName"": ""AppendToFile"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""path"",
""Value"": ""file.txt""},
{""Type"": ""String"",
""Name"": ""content"",
""Value"": ""%content%""},
{""Type"": ""String"",
""Name"": ""seperator"",
""Value"": ""\n""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("AppendToFile", gf.FunctionName);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("file.txt", gf.Parameters[0].Value);
			Assert.AreEqual("content", gf.Parameters[1].Name);
			Assert.AreEqual("%content%", gf.Parameters[1].Value);
			Assert.AreEqual("seperator", gf.Parameters[2].Name);
			Assert.AreEqual("\n", gf.Parameters[2].Value);

		}


		[DataTestMethod]
		[DataRow("copy %file1% to %file2%")]
		public async Task CopyFile_Test(string text)
		{
			string response = @"{""FunctionName"": ""CopyFile"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""sourceFileName"",
""Value"": ""%file1%""},
{""Type"": ""String"",
""Name"": ""destFileName"",
""Value"": ""%file2%""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("CopyFile", gf.FunctionName);
			Assert.AreEqual("sourceFileName", gf.Parameters[0].Name);
			Assert.AreEqual("%file1%", gf.Parameters[0].Value);
			Assert.AreEqual("destFileName", gf.Parameters[1].Name);
			Assert.AreEqual("%file2%", gf.Parameters[1].Value);
		}


		[DataTestMethod]
		[DataRow("delete %file1%")]
		public async Task DeleteFile_Test(string text)
		{
			string response = @"{""FunctionName"": ""DeleteFile"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""fileName"",
""Value"": ""%file1%""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("DeleteFile", gf.FunctionName);
			Assert.AreEqual("fileName", gf.Parameters[0].Name);
			Assert.AreEqual("%file1%", gf.Parameters[0].Value);
		}


		[DataTestMethod]
		[DataRow("get file info on %file%")]
		public async Task GetFileInfo_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetFileInfo"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""fileName"",
""Value"": ""%file%""}],
""ReturnValue"": {""Type"": ""IFileInfo"",
""VariableName"": ""fileInfo""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetFileInfo", gf.FunctionName);
			Assert.AreEqual("fileName", gf.Parameters[0].Name);
			Assert.AreEqual("%file%", gf.Parameters[0].Value);
			Assert.AreEqual("fileInfo", gf.ReturnValue.VariableName);

		}


		[DataTestMethod]
		[DataRow("create %dirName%")]
		public async Task CreateDirectory_Test(string text)
		{
			string response = @"{""FunctionName"": ""CreateDirectory"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""directoryPath"",
""Value"": ""%dirName%""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("CreateDirectory", gf.FunctionName);
			Assert.AreEqual("directoryPath", gf.Parameters[0].Name);
			Assert.AreEqual("%dirName%", gf.Parameters[0].Value);

		}


		[DataTestMethod]
		[DataRow("delete %dirName%")]
		public async Task DeleteDirectory_Test(string text)
		{
			string response = @"{""FunctionName"": ""DeleteDirectory"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""directoryPath"",
""Value"": ""%dirName%""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("DeleteDirectory", gf.FunctionName);
			Assert.AreEqual("directoryPath", gf.Parameters[0].Name);
			Assert.AreEqual("%dirName%", gf.Parameters[0].Value);

		}


		[DataTestMethod]
		[DataRow("does dir %dirName% exists, write to %dirExists%")]
		public async Task DirectoryExistsy_Test(string text)
		{
			string response = @"{""FunctionName"": ""DirectoryExists"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""directoryPath"",
""Value"": ""%dirName%""}],
""ReturnValue"": {""Type"": ""Boolean"",
""VariableName"": ""%dirExists%""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("DirectoryExists", gf.FunctionName);
			Assert.AreEqual("directoryPath", gf.Parameters[0].Name);
			Assert.AreEqual("%dirName%", gf.Parameters[0].Value);
			Assert.AreEqual("%dirExists%", gf.ReturnValue.VariableName);

		}
	}
}