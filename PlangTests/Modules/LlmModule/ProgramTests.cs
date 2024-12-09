using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.LlmModule;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Reflection;
using static PLang.Modules.BaseBuilder;

namespace PLangTests.Modules.LlmModule
{
    [TestClass]
	public class ProgramTests : BasePLangTest
	{
		Program p;
		MemoryStack memoryStack;
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
			memoryStack = new MemoryStack(pseudoRuntime, engine, settings, context);
		}


		private void SetupResponse(string response)
		{
			llmService.Query<object>(Arg.Any<LlmRequest>()).Returns(p =>
			{
				return (response, default(IError));
			});
			llmServiceFactory.CreateHandler().Returns(llmService);
			p = new Program(llmServiceFactory, identityService, settings, logger, context);
			p.Init(container, null, null, null, memoryStack, logger, context, typeHelper, llmServiceFactory, settings, appCache, null);
		}

		[TestMethod]
		public async Task AskLlm()
		{
			List<LlmMessage> messages = new();
			string scheme = null;
			string model = "gpt-4-test";
			double temperature = 0;
			double topP = 0;
			double frequencyPenalty = 0;
			double presencePenalty = 0;
			int maxLength = 4000;
			bool cacheResponse = true;
			string llmResponseType = "markup";

			SetupResponse(@"Hello world");

			var propertyInfo = typeof(Program).GetField("function", BindingFlags.NonPublic | BindingFlags.Instance);

			if (propertyInfo != null)
			{
				var rf = new List<ReturnValue>()
				{
					new ReturnValue("string", "markup")
				};
				var gf = new MethodExecution("AskLlm", new(), rf);
				propertyInfo.SetValue(p, gf); // Replace 'valueToSet' with the actual value you want to set
			}

			await p.AskLlm(messages, scheme, model, temperature, topP, frequencyPenalty, presencePenalty, maxLength, cacheResponse, llmResponseType);
			string markup = memoryStack.Get("markup").ToString();

			Assert.AreEqual("Hello world", markup);


		}


	}
}
