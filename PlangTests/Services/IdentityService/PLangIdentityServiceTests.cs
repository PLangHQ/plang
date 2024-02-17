using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Interfaces;
using PLang.Models;
using PLang.Utils;
using PLangTests;

namespace PLang.Services.IdentityService.Tests
{
	[TestClass()]
	public class PLangIdentityServiceTests : BasePLangTest
	{
		IPublicPrivateKeyCreator publicPrivateKeyCreator;
		PLangIdentityService pis;
		[TestInitialize]
		public void Init()
		{
			Initialize();

			publicPrivateKeyCreator = Substitute.For<IPublicPrivateKeyCreator>();

			var settings = new List<Setting>();
			var identites = new List<Identity>() {
					new Identity("default", "aaa", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40") { IsDefault = true },
					new Identity("default2", "aaaff", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40") { IsDefault = false }
				};
			settings.Add(new Setting("1", "PLang.Services.IdentityService.PLangIdentityService", typeof(string).ToString(), PLangIdentityService.SettingKey, JsonConvert.SerializeObject(identites)));

			settingsRepository = Substitute.For<ISettingsRepository>();
			settingsRepository.GetSettings().Returns(settings);

			publicPrivateKeyCreator.Create().Returns(new PublicPrivateKey("1234", "abcd"));


			pis = new PLangIdentityService(settingsRepository, publicPrivateKeyCreator, context);
		}


		[TestMethod()]
		public void CreateIdentityTest_NoIdentityExists()
		{
			var settings = new List<Setting>();
			settingsRepository.GetSettings().Returns(settings);

			publicPrivateKeyCreator.Create().Returns(new PublicPrivateKey("123", "abc"));

			var identity = pis.CreateIdentity();

			Assert.IsNotNull(identity);
			Assert.AreEqual("123", identity.Identifier);
			Assert.AreEqual("MyIdentity", identity.Name);
			Assert.AreEqual(true, identity.IsDefault);
			Assert.AreEqual(false, identity.IsArchived);
			Assert.AreEqual(null, identity.Value);
		}

		[TestMethod()]
		public void CreateIdentityTest_IdentityExistsNotDefault()
		{
			settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(new List<Identity>() { new Identity("default", "aaa", null) });


			var identity = pis.CreateIdentity("myIdentity");

			Assert.IsNotNull(identity);
			Assert.AreEqual("1234", identity.Identifier);
			Assert.AreEqual("myIdentity", identity.Name);
			Assert.AreEqual(false, identity.IsDefault);
			Assert.AreEqual(false, identity.IsArchived);
			Assert.AreEqual(null, identity.Value);
		}
		[TestMethod()]
		public void CreateIdentityTest_IdentityExistsSetNewAsDefault()
		{
			settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(
				new List<Identity>() {
					new Identity("default", "aaa", null) { IsDefault = true },
					new Identity("default2", "aaaff", null) { IsDefault = false }
				});


			var identity = pis.CreateIdentity("myIdentity", true);

			Assert.IsNotNull(identity);
			Assert.AreEqual("1234", identity.Identifier);
			Assert.AreEqual("myIdentity", identity.Name);
			Assert.AreEqual(true, identity.IsDefault);
			Assert.AreEqual(false, identity.IsArchived);
			Assert.AreEqual(null, identity.Value);
		}

		[TestMethod()]
		[ExpectedException(typeof(IdentityException))]
		public void CreateIdentityTest_IdentityAlreadyExists()
		{
			settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(new List<Identity>() { new Identity("default", "aaa", null) });


			var identity = pis.CreateIdentity("default");
		}
		[TestMethod()]
		public void GetIdentityTest()
		{
			var identity = pis.GetIdentity("default");

			Assert.IsNotNull(identity);
			Assert.AreEqual("aaa", identity.Identifier);
			Assert.AreEqual("default", identity.Name);
			Assert.AreEqual(true, identity.IsDefault);
			Assert.AreEqual(false, identity.IsArchived);
			Assert.AreEqual(null, identity.Value);

			var identity2 = pis.GetIdentity("default2");
			Assert.IsNotNull(identity2);
			Assert.AreEqual(identity2.Identifier, "aaaff");
			Assert.AreEqual(identity2.Name, "default2");
			Assert.AreEqual(identity2.IsDefault, false);
			Assert.AreEqual(identity2.IsArchived, false);
			Assert.AreEqual(identity2.Value, null);
		}
		[TestMethod()]
		[ExpectedException(typeof(IdentityException))]
		public void GetIdentityTest_NotFound()
		{

			var identity = pis.GetIdentity("default22");
		}

		[TestMethod()]
		public void SetIdentityTest()
		{
			settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(
				new List<Identity>() {
					new Identity("default", "aaa", null) { IsDefault = true },
					new Identity("default2", "aaaff", null) { IsDefault = false }
				});

			pis.SetIdentity("default2");

			var identity = pis.GetCurrentIdentity();
			Assert.IsNotNull(identity);
			Assert.AreEqual("aaaff", identity.Identifier);
			Assert.AreEqual("default2", identity.Name);
			Assert.AreEqual(false, identity.IsDefault);
			Assert.AreEqual(false, identity.IsArchived);
			Assert.AreEqual(null, identity.Value);
		}

		[TestMethod()]
		public void GetCurrentIdentityTest_NonHasBeenSet_UsesDefault()
		{
			settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(
				new List<Identity>() {
					new Identity("default", "aaa", null) { IsDefault = true },
					new Identity("default2", "aaaff", null) { IsDefault = false }
				});

			var identity = pis.GetCurrentIdentity();
			Assert.IsNotNull(identity);
			Assert.AreEqual("aaa", identity.Identifier);
			Assert.AreEqual("default", identity.Name);
			Assert.AreEqual(true, identity.IsDefault);
			Assert.AreEqual(false, identity.IsArchived);
			Assert.AreEqual(null, identity.Value);
		}
		[TestMethod()]
		public void GetCurrentIdentityTest_NoDefaultExistsSelectFirst()
		{
			
			var identity = pis.GetCurrentIdentity();
			Assert.IsNotNull(identity);
			Assert.AreEqual("aaa", identity.Identifier);
			Assert.AreEqual("default", identity.Name);
			Assert.AreEqual(true, identity.IsDefault);
			Assert.AreEqual(false, identity.IsArchived);
			Assert.AreEqual(null, identity.Value);
		}
		[TestMethod()]
		public void GetCurrentIdentityTest_CreateNewIdentity()
		{
			var settings = new List<Setting>();
			settingsRepository = Substitute.For<ISettingsRepository>();
			settingsRepository.GetSettings().Returns(settings);

			PLangIdentityService pis = new PLangIdentityService(settingsRepository, publicPrivateKeyCreator, context);
			var identity = pis.GetCurrentIdentity();
			Assert.IsNotNull(identity);
			Assert.AreEqual("1234", identity.Identifier);
			Assert.AreEqual("MyIdentity", identity.Name);
			Assert.AreEqual(true, identity.IsDefault);
			Assert.AreEqual(false, identity.IsArchived);
			Assert.AreEqual(null, identity.Value);
		}



		[TestMethod()]
		public void ArchiveIdentityTest()
		{		

			var identity = pis.ArchiveIdentity("default2");

			Assert.IsNotNull(identity);
		}

		[TestMethod()]
		[ExpectedException(typeof(IdentityException))]
		public void ArchiveIdentityTest_IsDefaultCannotBeArchived()
		{
			settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(
				new List<Identity>() {
					new Identity("default", "aaa", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40") { IsDefault = true },
					new Identity("default2", "aaaff", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40") { IsDefault = false }
				});

			PLangIdentityService pis = new PLangIdentityService(settingsRepository, publicPrivateKeyCreator, context);
			var identity = pis.ArchiveIdentity("default");

		}
	}
}