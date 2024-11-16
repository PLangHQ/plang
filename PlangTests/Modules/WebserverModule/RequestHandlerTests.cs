using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLangTests;

namespace PLang.Modules.WebserverModule.Tests;

[TestClass]
public class RequestHandlerTests : BasePLangTest
{
    [TestMethod]
    public void GetRoutingTest()
    {
        var webserverInfo = new WebserverInfo(null, null, null, null, 0, 0, false);
        webserverInfo.Routings.Add(new StaticFileRouting("/", "index.html"));
        var requestHandler = new RequestHandler(null, container, webserverInfo, null, null);
        var routing = requestHandler.GetRouting("/");

        Assert.IsNotNull(routing);
        Assert.AreEqual("/index.html", routing.Path);
    }
}