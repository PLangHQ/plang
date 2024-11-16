using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building.Model;
using PLangTests;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.ListDictionaryModule.Tests;

[TestClass]
public class BuilderTests : BasePLangTest
{
    private BaseBuilder builder;

    [TestInitialize]
    public void Init()
    {
        Initialize();

        LoadOpenAI();


        builder = new GenericFunctionBuilder();
        builder.InitBaseBuilder("PLang.Modules.ListDictionaryModule", fileSystem, llmServiceFactory, typeHelper,
            memoryStack, context, variableHelper, logger);
    }

    private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
    {
        var llmService = GetLlmService(stepText, caller, type);
        if (llmService == null) return;

        builder = new GenericFunctionBuilder();
        builder.InitBaseBuilder("PLang.Modules.ListDictionaryModule", fileSystem, llmServiceFactory, typeHelper,
            memoryStack, context, variableHelper, logger);
    }

    public GoalStep GetStep(string text)
    {
        var step = new GoalStep();
        step.Text = text;
        step.ModuleType = "PLang.Modules.ListDictionaryModule";
        return step;
    }


    [DataTestMethod]
    [DataRow("remove %item% from '%producDict%' dictionay")]
    public async Task DeleteKeyFromDictionaryy_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("DeleteKeyFromDictionary", gf.FunctionName);
        Assert.AreEqual("key", gf.Parameters[0].Name);
        Assert.AreEqual("%item%", gf.Parameters[0].Value);
        Assert.AreEqual("dictionary", gf.Parameters[1].Name);
        Assert.AreEqual("%producDict%", gf.Parameters[1].Value);
    }

    [DataTestMethod]
    [DataRow("add %item% to %productList%")]
    public async Task AddToList_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("AddToList", gf.FunctionName);
        Assert.AreEqual("value", gf.Parameters[0].Name);
        Assert.AreEqual("%item%", gf.Parameters[0].Value);
        Assert.AreEqual("listInstance", gf.Parameters[1].Name);
        Assert.AreEqual("%productList%", gf.Parameters[1].Value);
    }


    [DataTestMethod]
    [DataRow("add %productId%, %item% to dictionary %products%")]
    public async Task AddToDictionary_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("AddToDictionary", gf.FunctionName);
        Assert.AreEqual("key", gf.Parameters[0].Name);
        Assert.AreEqual("%productId%", gf.Parameters[0].Value);
        Assert.AreEqual("value", gf.Parameters[1].Name);
        Assert.AreEqual("%item%", gf.Parameters[1].Value);
        Assert.AreEqual("dictionaryInstance", gf.Parameters[2].Name);
        Assert.AreEqual("%products%", gf.Parameters[2].Value);
    }
}