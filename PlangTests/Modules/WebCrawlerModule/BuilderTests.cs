using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Modules;
using PLang.Modules.WebCrawlerModule;
using PLang.Services.OpenAi;
using PLang.Utils;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;

namespace PLangTests.Modules.WebCrawlerModule
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

            builder = new Builder();
            builder.InitBaseBuilder("PLang.Modules.WebCrawlerModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

        }


        private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
        {
            var llmService = GetLlmService(stepText, caller, type);
            if (llmService == null) return;

            builder = new Builder();
            builder.InitBaseBuilder("PLang.Modules.WebCrawlerModule", fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
        }

        public GoalStep GetStep(string text)
        {
            var step = new PLang.Building.Model.GoalStep();
            step.Text = text;
            step.ModuleType = "PLang.Modules.WebCrawlerModule";
            return step;
        }




        [DataTestMethod]
        [DataRow(@"- open example.org, use user session")]
        public async Task NavigateUrl_Test(string text)
        {
            SetupResponse(text);

            var step = GetStep(text);

            (var instruction, var error) = await builder.Build(step);
            var gf = instruction.Action as GenericFunction;

            Store(text, instruction.LlmRequest.RawResponse);


            Assert.AreEqual("NavigateToUrl", gf.FunctionName);
            Assert.AreEqual("url", gf.Parameters[0].Name);
            Assert.AreEqual("example.org", gf.Parameters[0].Value);
            Assert.AreEqual("useUserSession", gf.Parameters[1].Name);
            Assert.AreEqual(true, gf.Parameters[1].Value);
        }

        [DataTestMethod]
        [DataRow(@"- input %name% into #name")]
        public async Task Input_Test(string text)
        {
            SetupResponse(text);

            var step = GetStep(text);

            (var instruction, var error) = await builder.Build(step);
            var gf = instruction.Action as GenericFunction;

            Store(text, instruction.LlmRequest.RawResponse);


            Assert.AreEqual("Input", gf.FunctionName);
			Assert.AreEqual("value", gf.Parameters[0].Name);
			Assert.AreEqual("%name%", gf.Parameters[0].Value);
			Assert.AreEqual("cssSelector", gf.Parameters[1].Name);
            Assert.AreEqual("#name", gf.Parameters[1].Value);
        }


    }
}