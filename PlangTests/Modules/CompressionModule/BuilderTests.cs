using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Utils;
using PLangTests;
using System.Runtime.CompilerServices;
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

			settings.Get(typeof(OpenAiService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			LoadOpenAI();

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CompressionModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger, "");

		}

		private void SetupResponse(string stepText, [CallerMemberName] string caller = "", Type? type = null)
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.CompressionModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger, "");
		}
		public GoalStep GetStep(string text)
		{
			var step = new Building.Model.GoalStep();
			step.Text = text;
			step.ModuleType = "PLang.Modules.CompressionModule";
			return step;
		}
		[DataTestMethod]
		[DataRow("compress %filePath% to %destination%")]
		[DataRow("compress %filePath% to %destination% with high compression")]
		public async Task CompressFile_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("CompressFile", gf.FunctionName);
			Assert.AreEqual("filePath", gf.Parameters[0].Name);
			Assert.AreEqual("%filePath%", gf.Parameters[0].Value);
			Assert.AreEqual("saveToPath", gf.Parameters[1].Name);
			Assert.AreEqual("%destination%", gf.Parameters[1].Value);
			if (text.Contains("with high compression"))
			{
				Assert.AreEqual("compressionLevel", gf.Parameters[2].Name);
				Assert.AreEqual((long) 3, gf.Parameters[2].Value);
			}

		}


		[DataTestMethod]
		[DataRow("compress %dir% to %destination%, low compression, include all dir")]
		public async Task CompressDir_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);
			
			Assert.AreEqual("CompressDirectory", gf.FunctionName);
			Assert.AreEqual("sourceDirectoryName", gf.Parameters[0].Name);
			Assert.AreEqual("%dir%", gf.Parameters[0].Value);
			Assert.AreEqual("destinationArchiveFileName", gf.Parameters[1].Name);
			Assert.AreEqual("%destination%", gf.Parameters[1].Value);
			Assert.AreEqual("compressionLevel", gf.Parameters[2].Name);
			Assert.AreEqual((long) 0, gf.Parameters[2].Value);
			Assert.AreEqual("includeBaseDirectory", gf.Parameters[3].Name);
			Assert.AreEqual(true, gf.Parameters[3].Value);

		}


		[DataTestMethod]
		[DataRow("compress %files% to %destination%")]
		public async Task CompressFilePaths_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("CompressFiles", gf.FunctionName);
			Assert.AreEqual("filePaths", gf.Parameters[0].Name);
			Assert.AreEqual("%files%", gf.Parameters[0].Value);
			Assert.AreEqual("saveToPath", gf.Parameters[1].Name);
			Assert.AreEqual("%destination%", gf.Parameters[1].Value);

		}


		[DataTestMethod]
		[DataRow("uncompress %file% to %destination%")]
		public async Task UnCompressZipFile_Test(string text)
		{

			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("DecompressFile", gf.FunctionName);
			Assert.AreEqual("sourceArchiveFileName", gf.Parameters[0].Name);
			Assert.AreEqual("%file%", gf.Parameters[0].Value);
			Assert.AreEqual("destinationDirectoryName", gf.Parameters[1].Name);
			Assert.AreEqual("%destination%", gf.Parameters[1].Value);

		}
	}
}