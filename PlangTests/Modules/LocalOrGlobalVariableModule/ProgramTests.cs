using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PLangTests.Modules.LocalOrGlobalVariableModule;

[TestClass]
public class ProgramTests : BasePLangTest
{
    [TestInitialize]
    public void Init()
    {
        Initialize();
    }
}