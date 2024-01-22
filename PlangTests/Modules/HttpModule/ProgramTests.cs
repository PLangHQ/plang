using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.BlockchainModule;
using System.Net;
using System.Text;
using static PLang.Modules.BlockchainModule.ModuleSettings;

namespace PLangTests.Modules.HttpModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{

		public class TestHttpMessageHandler : HttpMessageHandler
		{
			private readonly string url;
			private readonly object data;
			private readonly bool signRequest;
			private readonly Dictionary<string, object>? headers;
			private readonly string encoding;
			private readonly string contentType;

			public TestHttpMessageHandler(string url, object data, bool signRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json")
			{
				this.url = url;
				this.data = data;
				this.signRequest = signRequest;
				this.headers = headers;
				this.encoding = encoding;
				this.contentType = contentType;
			}

			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Assert.AreEqual(request.RequestUri.ToString(), url);
				Assert.IsTrue(request.Headers.UserAgent.ToString().StartsWith("plang v"));
				Assert.AreEqual(signRequest, request.Headers.Contains("X-Signature"));
				Assert.AreEqual(signRequest, request.Headers.Contains("X-Signature-Contract"));
				Assert.AreEqual(signRequest, request.Headers.Contains("X-Signature-Created"));
				Assert.AreEqual(signRequest, request.Headers.Contains("X-Signature-Nonce"));
				Assert.AreEqual(signRequest, request.Headers.Contains("X-Signature-Address"));
				foreach (var header in headers)
				{
					var headerValue = request.Headers.FirstOrDefault(p => p.Key == header.Key).Value;
					Assert.AreEqual(header.Value, headerValue.ToList()[0]);
				}
				Assert.AreEqual(encoding, request.Content.Headers.ContentType.CharSet);
				Assert.AreEqual(contentType, request.Content.Headers.ContentType.MediaType);

				return new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.OK,
					Content = new StringContent("{\"key\":\"value\"}", Encoding.GetEncoding(encoding), contentType)
				};
			}


		}

		[TestInitialize]
		public void Init()
		{
			base.Initialize();
		}

		[TestMethod]
		public async Task Request_Test()
		{
			string url = "http://example.org/";
			object data = new { Name = "Stanley Hudson" };
			bool signRequest = true;
			var headers = new Dictionary<string, object>();
			headers.Add("X-Testing", "1");
			headers.Add("X-Testing-2", "2");
			string encoding = "utf-8";
			string contentType = "application/json";

			var wallets = new List<Wallet>();
			var rpcServers = new List<RpcServer>();
			settings.When(x => x.Set(typeof(ModuleSettings), "RpcServers", Arg.Any<List<RpcServer>>())).Do(callInfo =>
			{
				rpcServers = callInfo.Arg<List<RpcServer>>();
			});
			settings.When(p => p.SetList(typeof(ModuleSettings), Arg.Any<List<Wallet>>()))
				.Do((callback) =>
				{
					wallets = callback.Arg<List<Wallet>>();
				});
	
			settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(rpcServers);
			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(p =>
			{
				return wallets;
			});
			var httpClient = new HttpClient(new TestHttpMessageHandler(url, data, signRequest, headers, encoding, contentType));
			
			var p = new PLang.Modules.HttpModule.Program(fileSystem, signingService);
			var result = await p.Post(url, data, signRequest, headers, encoding, contentType);


		}

	}
}
