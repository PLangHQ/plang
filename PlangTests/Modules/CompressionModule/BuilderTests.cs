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

namespace PLang.Modules.CompressionModule.Tests
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
			builder.InitBaseBuilder("PLang.Modules.CompressionModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});			

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CompressionModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}

		[DataTestMethod]
		[DataRow("compress %filePath% to %destination%")]
		public async Task CompressFile_Test(string text)
		{
			string response = @"{""FunctionName"": ""CompressFile"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""filePath"",
""Value"": ""%filePath%""},
{""Type"": ""String"",
""Name"": ""saveToPath"",
""Value"": ""%destination%""},
{""Type"": ""CompressionLevel?"",
""Name"": ""compressionLevel"",
""Value"": ""Optimal""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("CompressFile", gf.FunctionName);
			Assert.AreEqual("filePath", gf.Parameters[0].Name);
			Assert.AreEqual("%filePath%", gf.Parameters[0].Value);
			Assert.AreEqual("saveToPath", gf.Parameters[1].Name);
			Assert.AreEqual("%destination%", gf.Parameters[1].Value);
			Assert.AreEqual("compressionLevel", gf.Parameters[2].Name);
			Assert.AreEqual("Optimal", gf.Parameters[2].Value);

		}


		[DataTestMethod]
		[DataRow("compress %dir% to %destination%")]
		public async Task CompressDir_Test(string text)
		{
			string response = @"{""FunctionName"": ""CompressDirectory"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""sourceDirectoryName"",
""Value"": ""%dir%""},
{""Type"": ""String"",
""Name"": ""destinationArchiveFileName"",
""Value"": ""%destination%""},
{""Type"": ""CompressionLevel?"",
""Name"": ""compressionLevel"",
""Value"": ""Optimal""},
{""Type"": ""Boolean?"",
""Name"": ""includeBaseDirectory"",
""Value"": ""True""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("CompressDirectory", gf.FunctionName);
			Assert.AreEqual("sourceDirectoryName", gf.Parameters[0].Name);
			Assert.AreEqual("%dir%", gf.Parameters[0].Value);
			Assert.AreEqual("destinationArchiveFileName", gf.Parameters[1].Name);
			Assert.AreEqual("%destination%", gf.Parameters[1].Value);
			Assert.AreEqual("compressionLevel", gf.Parameters[2].Name);
			Assert.AreEqual("Optimal", gf.Parameters[2].Value);
			Assert.AreEqual("includeBaseDirectory", gf.Parameters[3].Name);
			Assert.AreEqual("True", gf.Parameters[3].Value);

		}


		[DataTestMethod]
		[DataRow("compress %files% to %destination%")]
		public async Task CompressFilePaths_Test(string text)
		{
			string response = @"{""FunctionName"": ""CompressFiles"",
""Parameters"": [{""Type"": ""String[]"",
""Name"": ""filePaths"",
""Value"": ""%files%""},
{""Type"": ""String"",
""Name"": ""saveToPath"",
""Value"": ""%destination%""},
{""Type"": ""CompressionLevel?"",
""Name"": ""compressionLevel"",
""Value"": ""Optimal""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("CompressFiles", gf.FunctionName);
			Assert.AreEqual("filePaths", gf.Parameters[0].Name);
			Assert.AreEqual("%files%", gf.Parameters[0].Value);
			Assert.AreEqual("saveToPath", gf.Parameters[1].Name);
			Assert.AreEqual("%destination%", gf.Parameters[1].Value);
			Assert.AreEqual("compressionLevel", gf.Parameters[2].Name);
			Assert.AreEqual("Optimal", gf.Parameters[2].Value);

		}


		[DataTestMethod]
		[DataRow("uncompress %file% to %destination%")]
		public async Task UnCompressZipFile_Test(string text)
		{
			string response = @"{""FunctionName"": ""DecompressFiles"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""sourceArchiveFileName"",
""Value"": ""%file%""},
{""Type"": ""String"",
""Name"": ""destinationDirectoryName"",
""Value"": ""%destination%""}],
""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("DecompressFiles", gf.FunctionName);
			Assert.AreEqual("sourceArchiveFileName", gf.Parameters[0].Name);
			Assert.AreEqual("%file%", gf.Parameters[0].Value);
			Assert.AreEqual("destinationDirectoryName", gf.Parameters[1].Name);
			Assert.AreEqual("%destination%", gf.Parameters[1].Value);

		}
	}
}