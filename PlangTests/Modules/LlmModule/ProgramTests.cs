using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Models;
using PLang.Modules.LlmModule;
using PLang.Runtime;
using static PLang.Modules.BaseBuilder;

namespace PLangTests.Modules.LlmModule;

[TestClass]
public class ProgramTests : BasePLangTest
{
    private MemoryStack memoryStack;
    private Program p;

    [TestInitialize]
    public void Init()
    {
        Initialize();
        memoryStack = new MemoryStack(pseudoRuntime, engine, settings, context);
    }


    private void SetupResponse(string response)
    {
        llmService.Query<object>(Arg.Any<LlmRequest>()).Returns(p => { return (response, default); });
        llmServiceFactory.CreateHandler().Returns(llmService);
        p = new Program(llmServiceFactory, identityService, settings, logger, context);
        p.Init(container, null, null, null, memoryStack, logger, context, typeHelper, llmServiceFactory, settings,
            appCache, null);
    }

    [TestMethod]
    public async Task AskLlm()
    {
        List<LlmMessage> messages = new();
        string scheme = null;
        var model = "gpt-4-test";
        double temperature = 0;
        double topP = 0;
        double frequencyPenalty = 0;
        double presencePenalty = 0;
        var maxLength = 4000;
        var cacheResponse = true;
        var llmResponseType = "markup";

        SetupResponse(@"Hello world");

        var propertyInfo = typeof(Program).GetField("function", BindingFlags.NonPublic | BindingFlags.Instance);

        if (propertyInfo != null)
        {
            var rf = new List<ReturnValue>
            {
                new("string", "markup")
            };
            var gf = new GenericFunction("AskLlm", new List<Parameter>(), rf);
            propertyInfo.SetValue(p, gf); // Replace 'valueToSet' with the actual value you want to set
        }

        await p.AskLlm(messages, scheme, model, temperature, topP, frequencyPenalty, presencePenalty, maxLength,
            cacheResponse, llmResponseType);
        var markup = memoryStack.Get("markup").ToString();

        Assert.AreEqual("Hello world", markup);
    }
}