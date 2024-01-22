using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.IdentityService
{
	public interface IPublicPrivateKeyCreator
	{
		public PublicPrivateKey Create();
		
	}

	public class PublicPrivateKeyCreator : IPublicPrivateKeyCreator
	{
		public PublicPrivateKey Create()
		{
			var wordlist = Wordlist.English;

			var hdWallet = new Nethereum.HdWallet.Wallet(wordlist, WordCount.TwentyFour);
			var wallet = new PublicPrivateKey(hdWallet.GetEthereumKey(0).GetPublicAddress(), hdWallet.Seed);
			return wallet;
		}
	}

	public class PublicPrivateKey(string publicKey, string privateKey) : IDisposable
	{
		public void Dispose()
		{
			publicKey = null;
			privateKey = null;
		}

		public string GetPublicKey()
		{
			return publicKey;
		}

		public string GetPrivateKey()
		{
			return privateKey;
		}

	}
}
