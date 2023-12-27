using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Interfaces;
using PLang.Modules.BlockchainModule;
using PLangTests;
using System.Text;
using static PLang.Modules.BlockchainModule.ModuleSettings;

namespace PLang.Utils.Tests
{
	[TestClass()]
	public class HttpHelperTests : BasePLangTest
	{
		[TestInitialize]
		public void Init()
		{
			base.Initialize();
		}

		[TestMethod()]
		public void SignRequestTest()
		{
			HttpMethod method = HttpMethod.Get;
			string url = "https://dundermifflin.com";
			HttpRequestMessage request = new HttpRequestMessage(method, url);
			request.Content = new StringContent("hello", Encoding.UTF8);

			SystemTime.UtcNow = () => new DateTime(2000, 1, 1);
			
			var wallet = new Wallet("Default", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", "");
			List<Wallet> wallets = new List<Wallet>();
			wallets.Add(wallet);

			settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(new List<RpcServer>()
			{
				new RpcServer("Mumbai - Polygon testnet", "wss://polygon-bor.publicnode.com", 80001, true)
				{
					IsDefault = true
				}
			});
			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);


			httpHelper.SignRequest(request);

			Assert.AreEqual(HttpMethod.Get, method);
			Assert.AreEqual(request.RequestUri, url);

			request.Headers.TryGetValues("X-Signature-Contract", out var contracts);
			Assert.AreEqual("C0", contracts.FirstOrDefault());

			request.Headers.TryGetValues("X-Signature-Created", out var created);
			Assert.AreEqual(SystemTime.UtcNow().ToFileTimeUtc().ToString(), created.FirstOrDefault());

			var p = new Modules.BlockchainModule.Program(settings, context, aiService, null, null, memoryStack, null);
			string address = p.GetCurrentAddress().Result;


			request.Headers.TryGetValues("X-Signature-Address", out var addresses);
			Assert.AreEqual(address, addresses.FirstOrDefault());
			
			request.Headers.TryGetValues("X-Signature-Nonce", out var nonces);
			Assert.IsNotNull(nonces.FirstOrDefault());

		}

		[TestMethod()]
		public void VerifySignatureTest()
		{
			/*
			 * 
			 * Need to use HttpListenerRequest, not sure how to solve
			 * 
			var settings = container.GetInstance<ISettings>();
			settings.Set("Global_Wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40");
			string nonce = "2a828817-6294-48b3-9973-4a8d7017bb9c";


			string signature = "0x2b73f4fcac82e2e23ecda74835a33d054b4347924bf47ebf57d984a241b8629f68da35f0813210f10ffef03d31831d3582cc71dc54c3883b6bfe5e8548747c241b";
			HttpMethod method = HttpMethod.Get;
			string url = "https://dundermifflin.com";
			HttpListenerRequest request = new HttpListenerRequest(new Uri(url));

			request.Content = new StringContent("hello", Encoding.UTF8);

			SystemTime.UtcNow = () => new DateTime(2000, 1, 1);

			request.Headers.TryAddWithoutValidation("X-Signature", signature);
			request.Headers.TryAddWithoutValidation("X-Signature-Contract", "C0");
			request.Headers.TryAddWithoutValidation("X-Signature-Created", SystemTime.UtcNow().ToFileTimeUtc().ToString());
			request.Headers.TryAddWithoutValidation("X-Signature-Nonce", nonce);
			request.Headers.TryAddWithoutValidation("X-Signature-Address", "0x39AdD0ff2cb924fe6f268305324f3cBD9873A323");

			var memoryStack = new Runtime.MemoryStack();

			HttpHelper.VerifySignature(request, memoryStack);
			*/
		}
	}
}