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
			
			signingService = new PLangSigningService(appCache, identityService, context, serializer, crypto);

		}
		

		[TestMethod()]
		public async Task SignTest()
		{
			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";

			var headers = new Dictionary<string, object>();
			headers.Add("method", method);
			headers.Add("url", url);

			var signatureInfo = await signingService.Sign(body, [contract], null, headers);
			Assert.AreEqual("zEm+GE9Hhl08JspYtMfsibkqL2T+usZaZFLVCqlh4bO9rQWQL8+/YPnN5Vc38beo6tr3hHUQYkamRBsTNra9AQ==", signatureInfo.SignedData);
			Assert.AreEqual("zEm+GE9Hhl08JspYtMfsibkqL2T+usZaZFLVCqlh4bO9rQWQL8+/YPnN5Vc38beo6tr3hHUQYkamRBsTNra9AQ==", signatureInfo.SignedData);
			int i = 0;
		
		}

		[TestMethod()]
		public async Task ValidateSignature()
		{


			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string[] contract = ["C0"];

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
			var headers = new Dictionary<string, object>();
			headers.Add("method", method);
			headers.Add("url", url);

			var signature = await signingService.Sign(body, contracts: contract.ToList(), headers: headers);
			var base64Signature = await signingService.ToBase64(signature);


			var validationKeyValues = new Dictionary<string, object?>();
			validationKeyValues.Add("X-Signature", base64Signature);

			var xSignatureAsBase64 = validationKeyValues["X-Signature"].ToString();
			var recievedSignature = await signingService.FromBase64(xSignatureAsBase64);

			var result = await signingService.VerifySignature(recievedSignature);
			Assert.IsTrue(result.Signature.IsVerified);
			
		}

		[TestMethod()]
		[ExpectedException(typeof(SignatureExpiredException))]
		public async Task ValidateSignature_ToOldRequest()
		{

			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";
			/*
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
			*/
		}

		[TestMethod()]
		[ExpectedException(typeof(SignatureExpiredException))]
		public async Task ValidateSignature_SignatureExpired()
		{
			/*
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

			var result = await signingService.VerifySignature(body, method, url, signature
			*/
		}

		[TestMethod()]
		[ExpectedException(typeof(SignatureExpiredException))]
		public async Task ValidateSignature_NonceWasJustUsed()
		{
			/*
			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";

			var signature = signingService.Sign(body, method, url, contract);

			var result = await signingService.VerifySignature(body, method, url, signature);
			var result2 = await signingService.VerifySignature(body, method, url, signature);
			*/
		}

	}
}