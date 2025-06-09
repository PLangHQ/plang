using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Utils;
using PLangTests;
using PLangTests.Utils;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.CryptographicModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		BaseBuilder builder;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();

			LoadOpenAI();


			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

		}

		private void SetupResponse(string stepText, Type? type = null, [CallerMemberName] string caller = "")
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			builder = new GenericFunctionBuilder();
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
		}


		[DataTestMethod]
		[DataRow("encrypt %text%, write to %encryptedText%")]
		public async Task EcryptText(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("Encrypt", gf.Name);
			Assert.AreEqual("content", gf.Parameters[0].Name);
			Assert.AreEqual("%text%", gf.Parameters[0].Value);
			AssertVar.AreEqual("%encryptedText%", gf.ReturnValues[0].VariableName);
		}

		[DataTestMethod]
		[DataRow("decrypt %encryptedText%, write to %text%")]
		public async Task DecryptText(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("Decrypt", gf.Name);
			Assert.AreEqual("content", gf.Parameters[0].Name);
			Assert.AreEqual("%encryptedText%", gf.Parameters[0].Value);
			AssertVar.AreEqual("%text%", gf.ReturnValues[0].VariableName);
		}

		[DataTestMethod]
		[DataRow("hash %password%, write to %password%")]
		public async Task HashDefault_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("HashInput", gf.Name);
			Assert.AreEqual("input", gf.Parameters[0].Name);
			Assert.AreEqual("%password%", gf.Parameters[0].Value);
			Assert.AreEqual("useSalt", gf.Parameters[1].Name);
			Assert.AreEqual(true, gf.Parameters[1].Value);
			AssertVar.AreEqual("%password%", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("hash %text%, no salt, use sha256, write to %textHashed%")]
		public async Task HashUsingSha256_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("HashInput", gf.Name);
			Assert.AreEqual("input", gf.Parameters[0].Name);
			Assert.AreEqual("%text%", gf.Parameters[0].Value);
			Assert.AreEqual("useSalt", gf.Parameters[1].Name);
			Assert.AreEqual(false, gf.Parameters[1].Value);
			Assert.AreEqual("hashAlgorithm", gf.Parameters[2].Name);
			Assert.AreEqual("sha256", gf.Parameters[2].Value);
			AssertVar.AreEqual("%textHashed%", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("verify %password% matches %HashedPassword%, write to %isPasswordMatch%")]
		public async Task VerifyHashedBCrypt_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse); 
			
			Assert.AreEqual("VerifyHashedValues", gf.Name);
			Assert.AreEqual("text", gf.Parameters[0].Name);
			Assert.AreEqual("%password%", gf.Parameters[0].Value);
			Assert.AreEqual("hash", gf.Parameters[1].Name);
			Assert.AreEqual("%HashedPassword%", gf.Parameters[1].Value);
			AssertVar.AreEqual("isPasswordMatch", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("validate bearer %token%, write to %isValidToken%")]
		public async Task ValidateBearerToken_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse); 
			
			Assert.AreEqual("ValidateBearerToken", gf.Name);
			Assert.AreEqual("token", gf.Parameters[0].Name);
			Assert.AreEqual("%token%", gf.Parameters[0].Value);
			AssertVar.AreEqual("isValidToken", gf.ReturnValues[0].VariableName);

		}


		[DataTestMethod]
		[DataRow("generate bearer, %email%, valid for 15 minutes, write to %token%")]
		public async Task GenerateBearerToken_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("GenerateBearerToken", gf.Name);
			Assert.AreEqual("uniqueString", gf.Parameters[0].Name);
			Assert.AreEqual("%email%", gf.Parameters[0].Value);
			Assert.AreEqual("issuer", gf.Parameters[1].Name);
			Assert.AreEqual("PLangRuntime", gf.Parameters[1].Value);
			Assert.AreEqual("audience", gf.Parameters[2].Name);
			Assert.AreEqual("user", gf.Parameters[2].Value);
			Assert.AreEqual("expireTimeInSeconds", gf.Parameters[3].Name);
			Assert.AreEqual((long)900, gf.Parameters[3].Value);
			AssertVar.AreEqual("%token%", gf.ReturnValues[0].VariableName);

		}
	}
}