using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSec.Cryptography;
using NSubstitute;
using PLang.Interfaces;
using PLang.Services.IdentityService;
using PLang.Services.SettingsService;
using PLang.Utils;
using PLangTests;
using System.Text;

namespace PLang.Services.SigningService.Tests
{
	[TestClass()]
	public class PLangSigningServiceTests : BasePLangTest
	{
		PLangSigningService signingService;
		[TestInitialize]
		public void Init()
		{
			Initialize();


			identityService.GetCurrentIdentityWithPrivateKey().Returns(new Identity("MyIdentity", "Jgr2bN4rUi51cc44T0XOYIdsBx62kSSehj8IxBqhlgA=", "wDsnw/J1HfCj35ov/ysJbCh5Krj7rvNp3svxc0hoSjU=") { IsDefault = true });

			SystemTime.OffsetUtcNow = () =>
			{
				return new DateTimeOffset(new DateTime(2024, 01, 17));
			};
			SystemNonce.New = () =>
			{
				return "abc";
			};

			signingService = new PLangSigningService(appCache, identityService, context);

		}
		

		[TestMethod()]
		public async Task SignTest()
		{
			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";			

			var signatureInfo = signingService.Sign(body, method, url, contract);
			Assert.AreEqual("rJ3u5g2vaWiUYGuDClMoGMaI0yyhDmTxUqmL+4c3Vy0lX95qy55pNzgZNXo3RqKzZfGsrFuQfq9dyUYoJsKkAw==", signatureInfo["X-Signature"]);

		}

		[TestMethod()]
		public async Task ValidateSignature()
		{


			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";

			DateTime dt = DateTime.Now;
			string nonce = Guid.NewGuid().ToString();

			SystemTime.OffsetUtcNow = () =>
			{
				return dt;
			};
			SystemNonce.New = () =>
			{
				return nonce;
			};
			context.AddOrReplace(Settings.SaltKey, "123");


			var signature = signingService.Sign(body, method, url, contract);


			var validationKeyValues = new Dictionary<string, object>();
			validationKeyValues.Add("X-Signature", signature["X-Signature"]);
			validationKeyValues.Add("X-Signature-Created", SystemTime.OffsetUtcNow().ToUnixTimeMilliseconds());
			validationKeyValues.Add("X-Signature-Nonce", SystemNonce.New());
			validationKeyValues.Add("X-Signature-Public-Key", "Jgr2bN4rUi51cc44T0XOYIdsBx62kSSehj8IxBqhlgA=");
			validationKeyValues.Add("X-Signature-Contract", contract);

			var result = await signingService.VerifySignature(body, method, url, validationKeyValues);
			Assert.IsNotNull(result);
		}

		[TestMethod()]
		[ExpectedException(typeof(SignatureExpiredException))]
		public async Task ValidateSignature_ToOldRequest()
		{

			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";

			DateTime dt = DateTime.Now.AddMinutes(-5).AddSeconds(-1);
			SystemTime.OffsetUtcNow = () =>
			{
				return new DateTimeOffset(dt);
			};
			var signature = signingService.Sign(body, method, url, contract);


			SystemTime.OffsetUtcNow = () =>
			{
				return DateTimeOffset.UtcNow;
			};

			var result = await signingService.VerifySignature(body, method, url, signature);
		}

		[TestMethod()]
		[ExpectedException(typeof(SignatureExpiredException))]
		public async Task ValidateSignature_SignatureExpired()
		{

			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";

			DateTime dt = DateTime.Now.AddMinutes(-5).AddSeconds(-1);
			SystemTime.OffsetUtcNow = () =>
			{
				return new DateTimeOffset(dt);
			};

			DateTimeOffset expires = DateTimeOffset.UtcNow;

			var signature = signingService.SignWithTimeout(body, method, url, expires, contract);


			SystemTime.OffsetUtcNow = () =>
			{
				return DateTimeOffset.UtcNow.AddMinutes(1);
			};

			var result = await signingService.VerifySignature(body, method, url, signature);
		}

		[TestMethod()]
		[ExpectedException(typeof(SignatureExpiredException))]
		public async Task ValidateSignature_NonceWasJustUsed()
		{

			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";

			var signature = signingService.Sign(body, method, url, contract);

			var result = await signingService.VerifySignature(body, method, url, signature);
			var result2 = await signingService.VerifySignature(body, method, url, signature);
		}

	}
}