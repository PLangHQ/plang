using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Building.Model;
using PLangTests;
using PLangTests.Utils;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.FileModule.Tests;

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
        builder.InitBaseBuilder("PLang.Modules.FileModule", fileSystem, llmServiceFactory, typeHelper, memoryStack,
            context, variableHelper, logger);
    }

    private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
    {
        var llmService = GetLlmService(stepText, caller, type);
        if (llmService == null) return;

        builder = new GenericFunctionBuilder();
        builder.InitBaseBuilder("PLang.Modules.FileModule", fileSystem, llmServiceFactory, typeHelper, memoryStack,
            context, variableHelper, logger);
    }

    public GoalStep GetStep(string text)
    {
        var step = new GoalStep();
        step.Text = text;
        step.ModuleType = "PLang.Modules.FileModule";
        return step;
    }

    [DataTestMethod]
    [DataRow("read %mp4File% to %mp4ContentBase64%")]
    public async Task ReadBinaryFileAndConvertToBase64_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("ReadBinaryFileAndConvertToBase64", gf.FunctionName);
        Assert.AreEqual("path", gf.Parameters[0].Name);
        Assert.AreEqual("%mp4File%", gf.Parameters[0].Value);
        Assert.AreEqual("mp4ContentBase64", gf.ReturnValues[0].VariableName);
    }


    [DataTestMethod]
    [DataRow("read file.txt to %content%")]
    public async Task ReadTextFile_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("ReadTextFile", gf.FunctionName);
        Assert.AreEqual("path", gf.Parameters[0].Name);
        Assert.AreEqual("file.txt", gf.Parameters[0].Value);
        Assert.AreEqual("content", gf.ReturnValues[0].VariableName);
    }

    [DataTestMethod]
    [DataRow("read file.mp4 into %stream%")]
    public async Task ReadFileAsStream_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("ReadFileAsStream", gf.FunctionName);
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

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("ReadMultipleTextFiles", gf.FunctionName);
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

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("WriteToFile", gf.FunctionName);
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

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("AppendToFile", gf.FunctionName);
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

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("CopyFile", gf.FunctionName);
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

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("DeleteFile", gf.FunctionName);
        Assert.AreEqual("fileName", gf.Parameters[0].Name);
        Assert.AreEqual("%file1%", gf.Parameters[0].Value);
    }


    [DataTestMethod]
    [DataRow("get file info on %file%, write to %fileInfo")]
    public async Task GetFileInfo_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("GetFileInfo", gf.FunctionName);
        Assert.AreEqual("fileName", gf.Parameters[0].Name);
        Assert.AreEqual("%file%", gf.Parameters[0].Value);
        AssertVar.AreEqual("fileInfo", gf.ReturnValues[0].VariableName);
    }


    [DataTestMethod]
    [DataRow("create %dirName%")]
    public async Task CreateDirectory_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("CreateDirectory", gf.FunctionName);
        Assert.AreEqual("directoryPath", gf.Parameters[0].Name);
        Assert.AreEqual("%dirName%", gf.Parameters[0].Value);
    }


    [DataTestMethod]
    [DataRow("delete %dirName%")]
    public async Task DeleteDirectory_Test(string text)
    {
        SetupResponse(text);

        var step = GetStep(text);

        var (instruction, error) = await builder.Build(step);
        var gf = instruction.Action as GenericFunction;

        Store(text, instruction.LlmRequest.RawResponse);

        Assert.AreEqual("DeleteDirectory", gf.FunctionName);
        Assert.AreEqual("directoryPath", gf.Parameters[0].Name);
        Assert.AreEqual("%dirName%", gf.Parameters[0].Value);
    }
}