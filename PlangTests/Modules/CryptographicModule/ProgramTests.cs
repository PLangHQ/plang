using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.CryptographicModule;
using static PLang.Modules.CryptographicModule.ModuleSettings;

namespace PLangTests.Modules.CryptographicModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{

		[TestInitialize] 
		public void Init() {
			base.Initialize();
		}

		[TestMethod]
		public async Task HashUsing_And_VerifyHash_Test()
		{
			string password = "jfkla;sjfikwopefakl;asdf";

			var p = new Program(settings, context, encryption);
			var hash = await p.HashInput(password);

			var result = await p.VerifyHashedValues(password, hash);
			Assert.IsTrue(result);
		}
		[TestMethod]
		public async Task HashUsing_And_VerifyHash_NoSalt_Test()
		{
			string password = "jfkla;sjfikwopefakl;asdf";

			var p = new Program(settings, context, encryption);
			var hash = await p.HashInput(password, false);

			var result = await p.VerifyHashedValues(password, hash, useSalt: false);
			Assert.IsTrue(result);
		}


		[TestMethod]
		public async Task HashUsing_And_VerifyHash_WithFixedSalt_Test()
		{
			string password = "jfkla;sjfikwopefakl;asdf";

			var p = new Program(settings, context, encryption);
			var salt = BCrypt.Net.BCrypt.GenerateSalt();
			var hash = await p.HashInput(password, true, salt);

			var result = await p.VerifyHashedValues(password, hash);
			Assert.IsTrue(result);
		}

		[TestMethod]
		public async Task SetCurrentToken()
		{
			settings.GetValues<BearerSecret>(typeof(ModuleSettings)).Returns(new List<BearerSecret>()
			{
				new BearerSecret("Default", "wGl3A42CAMGEvsy5T11Jv7JqXKCLRsa5BJlPFZ1x2TI="),
				new BearerSecret("Default2", "2")
			});
			var p = new Program(settings, context, encryption);
			await p.SetCurrentBearerToken("Default2");
			var bearerSecret = await p.GetBearerSecret();
			Assert.AreEqual("2", bearerSecret);
		}

		[TestMethod]
		public async Task CreateBearerToken_And_Validate()
		{
			
			settings.GetValues<BearerSecret>(typeof(ModuleSettings)).Returns(new List<BearerSecret>()
			{
				new BearerSecret("Default", "wGl3A42CAMGEvsy5T11Jv7JqXKCLRsa5BJlPFZ1x2TI=")
			});
			/*
			var moduleSettings = new ModuleSettings(settings);
			moduleSettings.GenerateNewBearerSecretKey();
			*/

			var p = new Program(settings, context, encryption);
			var token = await p.GenerateBearerToken("email@example.org");
			var result = await p.ValidateBearerToken(token);

			Assert.IsTrue(result);
		}
	}
}
