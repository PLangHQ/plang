using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Utils;
using static PLang.Modules.IrohModule.Program;

namespace PLang.Modules.IrohModule.Tests
{
	[TestClass()]
	public class IrohPeerReaderTests
	{
		[TestMethod]
		public void AKeyAsText_IsAPeerThatIsNotConnected()
		{
			var peer = TypeHelper.ConvertToType("someEndpointKey", typeof(IrohPeer)) as IrohPeer;
			Assert.IsNotNull(peer);
			Assert.AreEqual("someEndpointKey", peer.Target);
			Assert.IsNull(peer.Connection);
		}
	}
}
