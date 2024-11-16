using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.DbModule;
using PLangTests;

namespace PLang.Utils.Tests;

[TestClass]
public class TypeHelperTests : BasePLangTest
{
    [TestInitialize]
    public void Init()
    {
        Initialize();
    }


    [TestMethod]
    public void GetMethodsTest()
    {
        var methods = typeHelper.GetMethodsAsString(typeof(Program));
        Assert.IsTrue(methods.Contains("Select") && methods.Contains("Insert"));

        methods = typeHelper.GetMethodsAsString(typeof(Modules.LocalOrGlobalVariableModule.Program));
        Assert.IsTrue(methods.Contains("SetVariable") && methods.Contains("SetDefaultValueOnVariables"));
    }

    [TestMethod]
    public void GetTypeTest()
    {
        var type = typeHelper.GetRuntimeType("PLang.Modules.CodeModule");

        Assert.IsTrue(typeof(Modules.CodeModule.Program) == type);
    }
}