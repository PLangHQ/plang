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
		private readonly ISettings settings;

		public Program(IPLangIdentityService identityService, IPLangSigningService signingService, ISettings settings)
		{
			this.identityService = identityService;
			this.signingService = signingService;
			this.settings = settings;
		}

		[Description("Get the current identity, also called %MyIdentity%")]
		public async Task<Identity> GetMyIdentity()
		{
			return identityService.GetCurrentIdentity();
		}
		[Description("Get an identity by name or identification")]
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

		[Description("Set the app to use shared identity. This is usefull when app is running on multiple location but want to use one identity for them all")]
		public async Task UseSharedIdentity()
		{
			identityService.UseSharedIdentity(settings.AppId);
		}

		public async Task RemoveSharedIdentity()
		{
			identityService.UseSharedIdentity(null);
		}

		[Description("Sign a content with specific method, url and contract. Returns key value object that contains the values to validate the signature")]
		public async Task<Dictionary<string, object>> Sign(string content, string method, string url, string contract = "C0")
		{
			return signingService.Sign(content, method, url, contract);
		}

		[Description("validationKeyValues should have these keys: X-Signature, X-Signature-Created(type is long, unix time), X-Signature-Nonce, X-Signature-Address, X-Signature-Contract=\"CO\". Return dictionary with Identity and IdentityNotHashed")]
		public async Task<Dictionary<string, object>?> VerifySignature(string content, string method, string url, Dictionary<string, object> validationKeyValues)
		{
			return await signingService.VerifySignature(content, method, url, validationKeyValues);
		}
	}
}
