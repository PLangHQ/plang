using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace PLang.Utils.Tests;

[TestClass]
public class StringHelperTests
{
    [TestMethod]
    public void ConvertToStringTest()
    {
        var i = 1;

        var result = StringHelper.ConvertToString(i);
        Assert.AreEqual("1", result);

        var str = "hello";
        result = StringHelper.ConvertToString(str);
        Assert.AreEqual(str, result);

        var json = @"{""name"": ""Ble""}";
        result = StringHelper.ConvertToString(json);
        Assert.AreEqual(json, result);

        json = @"{
  ""name"": ""Ble""
}";
        var jObject = JObject.Parse(json);
        result = StringHelper.ConvertToString(jObject);
        Assert.AreEqual(json, result);
    }
}