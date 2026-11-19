using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Modules.WebserverModule;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

		private static HttpContext Context(string method, string rawTarget, string path)
		{
			var httpContext = new DefaultHttpContext();
			httpContext.Request.Method = method;
			httpContext.Request.Path = new PathString(path);
			httpContext.Features.Set<IHttpRequestFeature>(new HttpRequestFeature { Method = method, RawTarget = rawTarget, Path = path });
			return httpContext;
		}

		[TestMethod]
		public void HandleEmptyPath_AsteriskForm_Options_AnswersWithAllow()
		{
			var httpContext = Context("OPTIONS", "*", "");

			Assert.IsTrue(RequestHandler.HandleEmptyPath(httpContext));
			Assert.AreEqual(200, httpContext.Response.StatusCode);
			StringAssert.Contains(httpContext.Response.Headers.Allow.ToString(), "OPTIONS");
		}

		[TestMethod]
		public void HandleEmptyPath_AsteriskForm_OtherMethod_IsBadRequest()
		{
			var httpContext = Context("GET", "*", "");

			Assert.IsTrue(RequestHandler.HandleEmptyPath(httpContext));
			Assert.AreEqual(400, httpContext.Response.StatusCode);
		}

		[TestMethod]
		public void HandleEmptyPath_AbsoluteFormWithoutPath_IsTheRoot()
		{
			var httpContext = Context("GET", "http://example.com", "");

			Assert.IsFalse(RequestHandler.HandleEmptyPath(httpContext));
			Assert.AreEqual("/", httpContext.Request.Path.Value);
		}

		[TestMethod]
		public void HandleEmptyPath_NormalPath_IsLeftAlone()
		{
			var httpContext = Context("GET", "/products", "/products");

			Assert.IsFalse(RequestHandler.HandleEmptyPath(httpContext));
			Assert.AreEqual("/products", httpContext.Request.Path.Value);
		}

		// The requests as Kestrel gets them from a socket: the empty path really arrives as an empty PathString.
		[DataTestMethod]
		[DataRow("OPTIONS * HTTP/1.1", 200, "Allow:")]
		[DataRow("GET http://localhost HTTP/1.1", 200, "path=/")]
		[DataRow("GET * HTTP/1.1", 405, "Allow: OPTIONS")] // Kestrel answers this one itself
		public async Task Kestrel_RequestWithEmptyPath_IsHandledNotAnError(string requestLine, int expectedStatus, string expectedInResponse)
		{
			if (OperatingSystem.IsWindows()) return;

			var builder = WebApplication.CreateBuilder();
			builder.Logging.ClearProviders();
			builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
			var app = builder.Build();
			app.Run(async httpContext =>
			{
				if (RequestHandler.HandleEmptyPath(httpContext)) return;
				await httpContext.Response.WriteAsync("path=" + httpContext.Request.Path.Value);
			});
			await app.StartAsync();
			try
			{
				var port = new Uri(app.Urls.First()).Port;
				using var client = new TcpClient();
				await client.ConnectAsync(IPAddress.Loopback, port);
				var stream = client.GetStream();
				var request = Encoding.ASCII.GetBytes($"{requestLine}\r\nHost: localhost\r\nConnection: close\r\n\r\n");
				await stream.WriteAsync(request);

				using var reader = new StreamReader(stream, Encoding.ASCII);
				var response = await reader.ReadToEndAsync();

				StringAssert.StartsWith(response, $"HTTP/1.1 {expectedStatus}");
				StringAssert.Contains(response, expectedInResponse);
			}
			finally
			{
				await app.StopAsync();
			}
		}
	}
}
