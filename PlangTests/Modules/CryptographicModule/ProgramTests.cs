using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.CryptographicModule;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.Security.Policy;
using static PLang.Modules.CryptographicModule.ModuleSettings;

namespace PLangTests.Modules.CryptographicModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{
		Program p;

		[TestInitialize] 
		public void Init() {
			base.Initialize();

			context.AddOrReplace(Settings.SaltKey, "123");
			p = new Program(settings, encryptionFactory, llmServiceFactory, fileSystem);
			p.Init(container, null, null, null, null);
		}

		[TestMethod]
		public async Task HashUsing_And_VerifyHash_Test()
		{
			string password = "jfkla;sjfikwopefakl;asdf";
			
			var hash = (await p.HashInput(password)).Hash;

			var result = await p.VerifyHashedValues(password, hash);
			Assert.IsTrue(result);
		}
		[TestMethod]
		public async Task HashUsing_And_VerifyHash_NoSalt_Test()
		{
			string password = "jfkla;sjfikwopefakl;asdf";

			var hash = (await p.HashInput(password, false)).Hash;

			var result = await p.VerifyHashedValues(password, hash, useSalt: false);
			Assert.IsTrue(result);
		}


		[TestMethod]
		public async Task HashUsing_And_VerifyHash_WithFixedSalt_Test()
		{
			string password = "jfkla;sjfikwopefakl;asdf";
			var salt = BCrypt.Net.BCrypt.GenerateSalt();
			var hash = (await p.HashInput(password, true, salt, "bcrypt")).Hash;

			var result = await p.VerifyHashedValues(password, hash, "bcrypt");
			Assert.IsTrue(result);
		}

		[TestMethod]
		public async Task SetCurrentToken()
		{
			settings.GetValues<Secret>(typeof(ModuleSettings)).Returns(new List<Secret>()
			{
				new Secret("Default", "wGl3A42CAMGEvsy5T11Jv7JqXKCLRsa5BJlPFZ1x2TI="),
				new Secret("Default2", "2")
			});

			await p.SetCurrentBearerToken("Default2");
			var bearerSecret = await p.GetBearerSecret();
			Assert.AreEqual("2", bearerSecret);
		}

		[TestMethod]
		public async Task CreateBearerToken_And_Validate()
		{
			
			settings.GetValues<Secret>(typeof(ModuleSettings)).Returns(new List<Secret>()
			{
				new Secret("Default", "wGl3A42CAMGEvsy5T11Jv7JqXKCLRsa5BJlPFZ1x2TI=")
			});
			var token = await p.GenerateBearerToken("email@example.org");
			var (result, error) = await p.ValidateBearerToken(token);

			Assert.IsTrue(result);
		}
	}
}
