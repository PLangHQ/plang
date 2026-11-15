using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PLang.Modules.IrohModule.Tests
{
	[TestClass]
	public class IrohKeysTests
	{
		[TestMethod]
		public void Generate_GivesSecretAndMatchingEndpointId()
		{
			var (secret, id) = IrohKeys.Generate();

			Assert.AreEqual(64, secret.Length);
			Assert.AreEqual(64, id.Length);
			Assert.AreEqual(id, IrohKeys.IdFromSecret(secret));
		}

		[TestMethod]
		public void Generate_TwoKeysAreDifferent()
		{
			var first = IrohKeys.Generate();
			var second = IrohKeys.Generate();

			Assert.AreNotEqual(first.Secret, second.Secret);
			Assert.AreNotEqual(first.Id, second.Id);
		}

		[TestMethod]
		public void IdFromSecret_RejectsAnInvalidSecret()
		{
			Assert.ThrowsException<IrohException>(() => IrohKeys.IdFromSecret("not a key"));
		}
	}
}
