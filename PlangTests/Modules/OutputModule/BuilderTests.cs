using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building.Model;
using PLangTests;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.OutputModule.Tests;

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
        builder.InitBaseBuilder("PLang.Modules.OutputModule", fileSystem, llmServiceFactory, typeHelper, memoryStack,
            context, variableHelper, logger);
    }


    private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
    {
        var llmService = GetLlmService(stepText, caller, type);
        if (llmService == null) return;

        builder = new GenericFunctionBuilder();
        builder.InitBaseBuilder("PLang.Modules.OutputModule", fileSystem, llmServiceFactory, typeHelper, memoryStack,
            context, variableHelper, logger);
    }

    public GoalStep GetStep(string text)
    {
        var step = new GoalStep();
        step.Text = text;
        step.ModuleType = "PLang.Modules.OutputModule";
        return step;
    }


    [DataTestMethod]
    [DataRow("ask, what should the settings be? write to %settings%")]
    public async Task Ask_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("Ask", gf.FunctionName);
        Assert.AreEqual("text", gf.Parameters[0].Name);
        Assert.AreEqual("what should the settings be?", gf.Parameters[0].Value);
        Assert.AreEqual("settings", gf.ReturnValues[0].VariableName);
    }

    [DataTestMethod]
    [DataRow("write out 'Hello PLang world'")]
    public async Task Write_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);
        Assert.AreEqual("Write", gf.FunctionName);
        Assert.AreEqual("content", gf.Parameters[0].Name);
        Assert.AreEqual("Hello PLang world", gf.Parameters[0].Value);
        Assert.AreEqual("writeToBuffer", gf.Parameters[1].Name);
        Assert.AreEqual(false, gf.Parameters[1].Value);
    }
}