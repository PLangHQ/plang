using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.HttpModule;

namespace PLangTests.Modules.HttpModule;

[TestClass]
public class ProgramTests : BasePLangTest
{
    private Program p;

    [TestInitialize]
    public void Init()
    {
        Initialize();

        p = new Program(fileSystem, signingService, httpClientFactory);

        p.Init(container, null, null, null, memoryStack, logger, context, typeHelper, llmServiceFactory, settings,
            appCache, null);
    }

    [TestMethod]
    public async Task Request_Test()
    {
        var url = "http://example.org/";
        object data = new { Name = "Stanley Hudson" };
        var doNotSignRequest = false;
        var headers = new Dictionary<string, object>();
        headers.Add("X-Testing", "1");
        headers.Add("X-Testing-2", "2");
        var encoding = "utf-8";
        var contentType = "application/json";

        signingService.Sign(Arg.Any<string>(), Arg.Any<string>(), "/").Returns(new Dictionary<string, object>());
        httpClientFactory.CreateClient()
            .Returns(new HttpClient(new TestHttpMessageHandler(url, data, doNotSignRequest, headers, encoding,
                contentType)));

        var result = await p.Post(url, data, doNotSignRequest, headers, encoding, contentType);
    }

    public class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly string contentType;
        private readonly object data;
        private readonly string encoding;
        private readonly Dictionary<string, object>? headers;
        private readonly bool signRequest;
        private readonly string url;

        public TestHttpMessageHandler(string url, object data, bool signRequest = false,
            Dictionary<string, object>? headers = null, string encoding = "utf-8",
            string contentType = "application/json")
        {
            this.url = url;
            this.data = data;
            this.signRequest = signRequest;
            this.headers = headers;
            this.encoding = encoding;
            this.contentType = contentType;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.AreEqual(request.RequestUri.ToString(), url);
            Assert.IsTrue(request.Headers.UserAgent.ToString().StartsWith("plang v"));
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
}