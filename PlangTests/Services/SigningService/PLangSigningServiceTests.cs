﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Interfaces;
using PLang.Services.IdentityService;
using PLang.Services.SettingsService;
using PLang.Utils;
using PLangTests;

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


			identityService.GetCurrentIdentityWithPrivateKey().Returns(new Identity("MyIdentity", "0x123", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40") { IsDefault = true });

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
			Assert.AreEqual("0x877335ab08164c667f26aad41b03a6be542cf5f445dfba988acc636345881099673f7affcdbacaf2d741674ad60ed4690720e533403e377b2e6267170a4d2b111b", signatureInfo["X-Signature"]);

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
			validationKeyValues.Add("X-Signature-Address", "0x39AdD0ff2cb924fe6f268305324f3cBD9873A323");
			validationKeyValues.Add("X-Signature-Contract", contract);

			var result = await signingService.VerifySignature("123", body, method, url, validationKeyValues);
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

			var result = await signingService.VerifySignature("123", body, method, url, signature);
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

			var result = await signingService.VerifySignature("123", body, method, url, signature);
		}

		[TestMethod()]
		[ExpectedException(typeof(SignatureExpiredException))]
		public async Task ValidateSignature_NonceWasJustUsed()
		{

			string body = "hello";
			string method = "POST";
			string url = "http://plang.is";
			string contract = "C0";
			context.AddOrReplace(Settings.SaltKey, "123");
			var signature = signingService.Sign(body, method, url, contract);

			var result = await signingService.VerifySignature("123", body, method, url, signature);
			var result2 = await signingService.VerifySignature("123", body, method, url, signature);
		}

	}
}