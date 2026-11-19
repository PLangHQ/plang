using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PLangTests.Modules.WebserverModule
{
	[TestClass]
	public class EmptyPathTests
	{
		// RequestHandler.GetGoalByRouting used to test `request.Path == null`. For a PathString that is true for an
		// empty path, which a request such as `OPTIONS *` or `GET http://host` has, so such a request was a 500 "request object empty".
		[TestMethod]
		public void PathString_EmptyPath_EqualsNull()
		{
			var request = new DefaultHttpContext().Request;
			request.Path = new PathString("");

			Assert.IsTrue(request.Path == null);
			Assert.IsFalse(request.Path.HasValue);
		}

		[TestMethod]
		public void PathString_NormalPath_IsNotNull()
		{
			var request = new DefaultHttpContext().Request;
			request.Path = new PathString("/");

			Assert.IsFalse(request.Path == null);
		}
	}
}
