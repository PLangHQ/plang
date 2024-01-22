using PLang.Interfaces;
using PLang.Model;
using PLang.Modules;
using PLang.Services.SigningService;
using System.ComponentModel;

namespace PLang.Modules.IdentityModule
{
	[Description("Handles Identity in plang. Sign content and verifies signature with the identity")]
	public class Program : BaseProgram
	{
		private readonly IPLangIdentityService identityService;
		private readonly IPLangSigningService signingService;

		public Program(IPLangIdentityService identityService, IPLangSigningService signingService)
		{
			this.identityService = identityService;
			this.signingService = signingService;
		}

		[Description("Get the current identity, also called %MyIdentity%")]
		public async Task<Identity> GetMyIdentity()
		{
			return identityService.GetCurrentIdentity();
		}
		[Description("Get the current identity, also called %MyIdentity%")]
		public async Task<Identity> GetIdentity(string nameOrIdentity) 
		{
			return identityService.GetIdentity(nameOrIdentity);
		}
		[Description("Create a new identity in the system")]
		public async Task<Identity> CreateIdentity(string name, bool setAsDefault = false)
		{
			return identityService.CreateIdentity(name, setAsDefault);
		}

		[Description("Set the current identity(MyIdentity). Name or identity, identity could be a public address, 0x123....")]
		public async Task<Identity> SetCurrentIdentity(string nameOrIdentity)
		{
			return identityService.SetIdentity(nameOrIdentity);
		}
		[Description("Archives a identity. Name or identity, identity could be a public address, 0x123....")]
		public async Task<Identity?> ArchiveIdentity(string nameOrIdentity)
		{
			return identityService.ArchiveIdentity(nameOrIdentity);
		}

		[Description("Gets all identites in the system")]
		public async Task<IEnumerable<Identity>> GetIdentities()
		{
			return identityService.GetIdentities();
		}

		[Description("Sign a content with specific method, url and contract. Return SignatureInfo object that contains .Signature and .KeyValues that are the values to create the signature")]
		public async Task<Dictionary<string, object>> Sign(string content, string method, string url, string contract = "C0")
		{
			return signingService.Sign(content, method, url, contract);
		}

		[Description("validationKeyValues should have these keys: X-Signature, X-Signature-Created(type is long, unix time), X-Signature-Nonce, X-Signature-Address, X-Signature-Contract=\"CO\"")]
		public async Task<string?> VerifySignature(string content, string method, string url, Dictionary<string, object> validationKeyValues)
		{
			return await signingService.VerifySignature(content, method, url, validationKeyValues);
		}
	}
}
