using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PLang.Utils.Tests;

[TestClass]
public class DateTimeExtensionTests
{
    [TestMethod]
    public void GetUnixTimeTest()
    {
        var now = DateTime.Now;
        var result = now.GetUnixTime();
        Assert.IsTrue(result > 0);

        var start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(0, start.GetUnixTime());
    }
}