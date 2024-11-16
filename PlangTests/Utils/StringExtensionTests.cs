using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PLang.Utils.Tests;

[TestClass]
public class StringExtensionTests
{
    [TestMethod]
    public void BetweenTest()
    {
        var str = "DataSource=.db/system.sqlite;Version=3";
        var path = str.Between("=", ";");
        Assert.AreEqual(".db/system.sqlite", path);
    }
}