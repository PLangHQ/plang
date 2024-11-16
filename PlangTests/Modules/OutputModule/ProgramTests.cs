using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.OutputModule;

namespace PLangTests.Modules.OutputModule;

[TestClass]
public class ProgramTests : BasePLangTest
{
    [TestInitialize]
    public void Init()
    {
        Initialize();
    }

    [TestMethod]
    public async Task Ask_Test()
    {
        outputStream.Ask(Arg.Any<string>()).Returns("good");
        var p = new Program(outputStreamFactory, outputSystemStreamFactory);
        var result = await p.Ask("Hello, how are your?");

        Assert.AreEqual("good", result);
    }

    [TestMethod]
    public async Task Write_Test()
    {
        var p = new Program(outputStreamFactory, outputSystemStreamFactory);
        await p.Write("Hello, how are your?");

        await outputStream.Received(1).Write(Arg.Any<object>());
    }
}