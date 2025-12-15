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
using PLangTests.Utils;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

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

			LoadOpenAI();

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
		}


		[DataTestMethod]
		[DataRow("read %mp4File% to %mp4ContentBase64%")]
		public async Task ReadBinaryFileAndConvertToBase64_Test(string text)
		{
			SetupResponse(text);
			 
			LoadStep(text);		

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("ReadBinaryFileAndConvertToBase64", gf.Name);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("%mp4File%", gf.Parameters[0].Value);
			Assert.AreEqual("mp4ContentBase64", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("read file.txt to %content%")]
		public async Task ReadTextFile_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("ReadTextFile", gf.Name);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("file.txt", gf.Parameters[0].Value);
			Assert.AreEqual("content", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("read file.mp4 into %stream%")]
		public async Task ReadFileAsStream_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("ReadFileAsStream", gf.Name);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("file.mp4", gf.Parameters[0].Value);
			Assert.AreEqual("stream", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("read all files in %dir% and subfolders, into %contents%", "*", true)]
		[DataRow("read all files in %dir% ending with mp4, dont include sub dirs, into %contents%", "*.mp4", false)]
		public async Task ReadMultipleTextFiles_Test(string text, string pattern, bool includeSubFolders)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("ReadMultipleTextFiles", gf.Name);
			Assert.AreEqual("folderPath", gf.Parameters[0].Name);
			Assert.AreEqual("%dir%", gf.Parameters[0].Value);
			if (text.Contains("and subfolders"))
			{
				Assert.AreEqual("includeAllSubfolders", gf.Parameters[1].Name);
				Assert.AreEqual(includeSubFolders, gf.Parameters[1].Value);
			}
			else
			{
				Assert.AreEqual("searchPattern", gf.Parameters[1].Name);
				Assert.AreEqual(pattern, gf.Parameters[1].Value);
				Assert.AreEqual("includeAllSubfolders", gf.Parameters[2].Name);
				Assert.AreEqual(includeSubFolders, gf.Parameters[2].Value);
			}
			AssertVar.AreEqual("%contents%", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("write %content% to file.txt, overwrite it")]
		public async Task WriteToFile_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("WriteToFile", gf.Name);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("file.txt", gf.Parameters[0].Value);
			Assert.AreEqual("content", gf.Parameters[1].Name);
			Assert.AreEqual("%content%", gf.Parameters[1].Value);
			Assert.AreEqual("overwrite", gf.Parameters[2].Name);
			Assert.AreEqual(true, gf.Parameters[2].Value);

		}


		[DataTestMethod]
		[DataRow("append %content% to file.txt")]
		[DataRow("append %content% to file.txt, seperator -")]
		public async Task AppendToFile_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("AppendToFile", gf.Name);
			Assert.AreEqual("path", gf.Parameters[0].Name);
			Assert.AreEqual("file.txt", gf.Parameters[0].Value);
			Assert.AreEqual("content", gf.Parameters[1].Name);
			Assert.AreEqual("%content%", gf.Parameters[1].Value);
			if (text.Contains("seperator"))
			{
				Assert.AreEqual("seperator", gf.Parameters[2].Name);
				Assert.AreEqual("-", gf.Parameters[2].Value);
			}

		}


		[DataTestMethod]
		[DataRow("copy %file1% to %file2%")]
		public async Task CopyFile_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("CopyFile", gf.Name);
			Assert.AreEqual("sourceFileName", gf.Parameters[0].Name);
			Assert.AreEqual("%file1%", gf.Parameters[0].Value);
			Assert.AreEqual("destFileName", gf.Parameters[1].Name);
			Assert.AreEqual("%file2%", gf.Parameters[1].Value);
		}


		[DataTestMethod]
		[DataRow("delete %file1%")]
		public async Task DeleteFile_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("DeleteFile", gf.Name);
			Assert.AreEqual("fileName", gf.Parameters[0].Name);
			Assert.AreEqual("%file1%", gf.Parameters[0].Value);
		}


		[DataTestMethod]
		[DataRow("get file info on %file%, write to %fileInfo")]
		public async Task GetFileInfo_Test(string text)
		{
			SetupResponse(text); 

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("GetFileInfo", gf.Name);
			Assert.AreEqual("fileName", gf.Parameters[0].Name);
			Assert.AreEqual("%file%", gf.Parameters[0].Value);
			AssertVar.AreEqual("fileInfo", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("create %dirName%")]
		public async Task CreateDirectory_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("CreateDirectory", gf.Name);
			Assert.AreEqual("directoryPath", gf.Parameters[0].Name);
			Assert.AreEqual("%dirName%", gf.Parameters[0].Value);

		}


		[DataTestMethod]
		[DataRow("delete %dirName%")]
		public async Task DeleteDirectory_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("DeleteDirectory", gf.Name);
			Assert.AreEqual("directoryPath", gf.Parameters[0].Name);
			Assert.AreEqual("%dirName%", gf.Parameters[0].Value);

		}


	}
}