using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.ListDictionaryModule;

namespace PLangTests.Modules.ListDictionaryModule;

[TestClass]
public class ProgramTests : BasePLangTest
{
    private Program p;

    [TestInitialize]
    public void Init()
    {
        Initialize();
        p = new Program();
    }
    /*


    [TestMethod]
    public async Task DeleteObjectFromDictionary_Test()
    {
        var dict = new Dictionary<string, object>();
        dict.Add("1", "a");
        dict.Add("2", "b");
        dict.Add("3", "c");

        memoryStack.PutStatic("dict", dict);

        await p.DeleteObjectFromDictionary("dict", "2", true);
        var outDict = await p.GetDictionary("dict", true) as Dictionary<string, object>;
        Assert.AreEqual(2, outDict.Count);
    }

    */
}