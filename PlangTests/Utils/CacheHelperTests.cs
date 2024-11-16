using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLangTests;

namespace PLang.Utils.Tests;

[TestClass]
public class CacheHelperTests : BasePLangTest
{
    [TestInitialize]
    public void Init()
    {
        Initialize();
    }
    /*
     * Change to LlmRequest
    [TestMethod()]
    public void GetStringToHashTest()
    {
        var llmQuestion = new Building.Model.LlmQuestion("1", "2", "3", "4");
        var result = cacheHelper.GetStringToHash(llmQuestion);
        Assert.IsNotNull(result);
        Assert.AreEqual("1243gpt-4", result);
    }

    [TestMethod()]
    public void GetCachedQuestionTest()
    {
        var llmQuestion = new Building.Model.LlmQuestion("1", "2", "3", "4");
        var result = cacheHelper.GetCachedQuestion(llmQuestion);
        Assert.IsNull(result);
    }

    [TestMethod()]
    public void SetCachedQuestionTest()
    {
        var llmQuestion = new Building.Model.LlmQuestion("1", "2", "3", "4");
        llmQuestion.RawResponse = "5";
        cacheHelper.SetCachedQuestion(llmQuestion);
        var result = cacheHelper.GetCachedQuestion(llmQuestion);
        Assert.IsNotNull(result);

    }
    */
}