﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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

namespace PLang.Modules.HttpModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		BaseBuilder builder;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();

			settings.Get(typeof(OpenAiService), "OpenAiKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var llmService = new OpenAiService(settings, logger, llmCaching, context);
			llmServiceFactory.CreateHandler().Returns(llmService);
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.HttpModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.HttpModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
		}
		public GoalStep GetStep(string text)
		{
			var step = new Building.Model.GoalStep();
			step.Text = text;
			step.ModuleType = "PLang.Modules.HttpModule";
			return step;
		}


		[DataTestMethod]
		[DataRow("get http://example.org, write to %json%")]
		public async Task Get_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("Get", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			AssertVar.AreEqual("json", gf.ReturnValues[0].VariableName);

		}



		[DataTestMethod]
		[DataRow("POST http://example.org, write to %json%")]
		public async Task Post_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("Post", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			AssertVar.AreEqual("json", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("Patch http://example.org, write to %json%")]
		public async Task Patch_Test(string text)
		{

			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("Patch", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			AssertVar.AreEqual("json", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("delete http://example.org, write to %json%")]
		public async Task Delete_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("Delete", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			AssertVar.AreEqual("json", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("put http://example.org, write to %json%")]
		public async Task Put_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("Put", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			AssertVar.AreEqual("json", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("Head http://example.org, write to %json%")]
		public async Task Head_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("Head", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			AssertVar.AreEqual("json", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("Options http://example.org, write to %json%")]
		public async Task Options_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("Option", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			AssertVar.AreEqual("json", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("post http://example.org, file=%@file%, write to %json%")]
		public async Task Post_Multipart_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("PostMultipartFormData", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("http://example.org", gf.Parameters[0].Value);
			AssertVar.AreEqual("%json%", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("download https://file-examples.com/wp-content/storage/2017/02/file_example_JSON_1kb.json, and save to file.json")]
		public async Task Download_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("DownloadFile", gf.FunctionName);
			Assert.AreEqual("url", gf.Parameters[0].Name);
			Assert.AreEqual("https://file-examples.com/wp-content/storage/2017/02/file_example_JSON_1kb.json", gf.Parameters[0].Value);
			Assert.AreEqual("pathToSaveTo", gf.Parameters[1].Name);
			Assert.AreEqual("file.json", gf.Parameters[1].Value);
			
		}
	}
}