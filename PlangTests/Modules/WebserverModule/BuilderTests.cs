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

namespace PLang.Modules.WebserverModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		BaseBuilder builder;

		[TestInitialize]
		public void Init()
		{
			Initialize();

			LoadOpenAI();
			
			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.WebserverModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger, "");

		}


		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder("PLang.Modules.WebserverModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger, "");
		}

		public GoalStep GetStep(string text)
		{
			var step = new PLang.Building.Model.GoalStep();
			step.Text = text;
			step.ModuleType = "PLang.Modules.WebserverModule";
			return step;
		}



		[DataTestMethod]
		[DataRow("start webserver, 8080, [api, user]")]
		public async Task StartWebserver_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("StartWebserver", gf.FunctionName);
			Assert.AreEqual("port", gf.Parameters[0].Name);
			Assert.AreEqual((long) 8080, gf.Parameters[0].Value);

			Assert.AreEqual("publicPaths", gf.Parameters[1].Name);

			var paths = JsonConvert.DeserializeObject<string[]>(gf.Parameters[1].Value.ToString());

			Assert.AreEqual("api", paths[0]);
			Assert.AreEqual("user", paths[1]);

		}



		[DataTestMethod]
		[DataRow("get user ip, write to %ip%")]
		public async Task GetUserIp_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("GetUserIp", gf.FunctionName);
			Assert.AreEqual("headerKey", gf.Parameters[0].Name);
			Assert.AreEqual(null, gf.Parameters[0].Value);

			Assert.AreEqual("ip", gf.ReturnValues[0].VariableName);

		}



		[DataTestMethod]
		[DataRow("write header 'X-Set-Data' as value 123")]
		public async Task SetHeader_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("WriteToResponseHeader", gf.FunctionName);
			Assert.AreEqual("headers", gf.Parameters[0].Name);

			var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(gf.Parameters[0].Value.ToString());

			Assert.AreEqual("X-Set-Data", dict.FirstOrDefault().Key);
			Assert.AreEqual("123", dict.FirstOrDefault().Value);

		}



		[DataTestMethod]
		[DataRow("get cache-control header, write to %cacheControl%")]
		public async Task GetRequestHeader_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("GetRequestHeader", gf.FunctionName);
			Assert.AreEqual("key", gf.Parameters[0].Name);
			Assert.AreEqual("cache-control", gf.Parameters[0].Value);

			Assert.AreEqual("cacheControl", gf.ReturnValues[0].VariableName);

		}



		[DataTestMethod]
		[DataRow("get cookie 'TOS', write to %cookieValue%")]
		public async Task GetCookie_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("GetCookie", gf.FunctionName);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("TOS", gf.Parameters[0].Value);

			AssertVar.AreEqual("%cookieValue%", gf.ReturnValues[0].VariableName);

		}





		[DataTestMethod]
		[DataRow("set cookie 'service' to 1")]
		public async Task SetCookie_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("WriteCookie", gf.FunctionName);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("service", gf.Parameters[0].Value);
			Assert.AreEqual("value", gf.Parameters[1].Name);
			Assert.AreEqual("1", gf.Parameters[1].Value);

		}



		[DataTestMethod]
		[DataRow("delete cookie 'service'")]
		public async Task DeleteCookie_Test(string text)
		{
			SetupResponse(text);

			var step = GetStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Store(text, instruction.LlmRequest.RawResponse);

			Assert.AreEqual("DeleteCookie", gf.FunctionName);
			Assert.AreEqual("name", gf.Parameters[0].Name);
			Assert.AreEqual("service", gf.Parameters[0].Value);

		}

	}
}