using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.WebserverModule;
using PLangTests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules.WebserverModule.Tests
{
	[TestClass()]
	public class RequestHandlerTests : BasePLangTest
	{
		[TestMethod()]
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
}