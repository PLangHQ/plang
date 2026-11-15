using PLang.Errors.AskUser;
using PLang.Errors.Runtime;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Model;
using PLang.Modules;
using PLang.Services.EncryptionService;
using PLang.Services.LlmService;
using PLang.Services.SigningService;
using PLang.Utils;
using System.ComponentModel;
using static PLang.Errors.AskUser.AskUserPrivateKeyExport;
using PLang.Utils.Extractors;
using PLang.Models;
using Identity = PLang.Interfaces.Identity;
using System.Collections;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PLang.Modules.IdentityModule
{
	[Description("Handles Identity in plang. Sign content and verifies signature with the identity.")]
	[PLang.Attributes.Effect(PLang.Attributes.Effects.None)]
	public class Program : BaseProgram
	{
		private readonly IPLangIdentityService identityService;
		private readonly IPLangSigningService signingService;
		private readonly ISettings settings;
		private readonly ILlmServiceFactory llmServiceFactory;

		public Program(IPLangIdentityService identityService, IPLangSigningService signingService, ISettings settings, ILlmServiceFactory llmServiceFactory)
		{
			this.identityService = identityService;
			this.signingService = signingService;
			this.settings = settings;
			this.llmServiceFactory = llmServiceFactory;
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

		[Description("Sign a object to a specific property on that object. Returns signature object that contains the values to validate the signature")]
		public async Task<(object?, IError?)> SignIntoProperty(object body, string property)
		{
			if (body == null)
			{
				return (null, new ProgramError("Variable to sign is empty"));
			}

			var signature = await signingService.Sign(body);
			if (body is IDictionary dict)
			{
				dict.Add(property, signature);
				return (dict, null);
			} else
			{
				var obj = JObject.FromObject(body);
				obj.Add(property, JToken.FromObject(signature));
				return (obj, null);	
			}

				return (null, new ProgramError($"Not supported body type {body.GetType()}"));
		}

		[Description("Sign a content with specific headers and contracts. Returns signature object that contains the values to validate the signature")]
		public async Task<SignedMessage> Sign(object? body, List<string>? contracts = null, int? expiresInSeconds = null, Dictionary<string, object>? headers = null, bool skipNonce = false)
		{
			return await signingService.Sign(body, contracts, expiresInSeconds, headers, skipNonce: skipNonce);
		}

		[Description("Validate a signature on specific properties")]
		public async Task<(SignedMessage? Signature, IError? Error)> VerifySignatureOnProperties(object? signatureFromUser, List<string> properties)
		{
			return (null, new ProgramError("Not supported"));
		}

		[Description("Validate a signature. Return the signature when valid, gives error when invalid")]
		public async Task<(SignedMessage? Signature, IError? Error)> VerifySignature(object? signatureFromUser = null, Dictionary<string, object?>? headers = null, object? body = null, List<string>? contracts = null,
			[Description("true when the step says the signature must cover the body, e.g. `body is required`. Without it a signature that carries no hash of its data is accepted for any body")] bool requireData = false)
		{
			if (signatureFromUser == null)
			{
				return (null, new ProgramError("Signature is missing"));
			}
			SignedMessage? signature = signatureFromUser as SignedMessage;
			if (signature == null && signatureFromUser is JToken token)
			{
				// A signature that came back as JSON (tell, http, a file) is a JObject by the time it gets here.
				try { signature = token.ToObject<SignedMessage>(); }
				catch (Exception ex) { return (null, new ProgramError("Signature could not be converted to proper Signature: " + ex.Message, goalStep)); }
			}
			if (signature == null && signatureFromUser is string str)
			{
				signature = SignatureCreator.Parse(str);
			} else if (signature == null && signatureFromUser is IDictionary)
			{
				var result = SignatureCreator.Cast(signatureFromUser as Dictionary<string, object?>);
				if (result.Error != null) return (null, result.Error);
				signature = result.Signature!;
			}

			if (signature == null)
			{
				return (null, new ProgramError("Signature could not be converted to proper Signature"));
			}	

			if (requireData && signature.Data == null)
			{
				return (null, new ProgramError("The signature does not cover any data (no data hash), so it proves nothing about the body.", goalStep, Key: "SignatureInvalid"));
			}

			return await signingService.VerifySignature(signature, headers, body, contracts);
		}

		public const string SignatureFileExtension = ".signature";
		public const string FileContract = "file/1";

		/// <summary>What a file signature signs: the file's name and the sha256 of the bytes on disk (a BOM included), nothing else.</summary>
		private Dictionary<string, object> FileSignatureData(string absolutePath)
		{
			using var stream = fileSystem.File.OpenRead(absolutePath);
			string sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
			return new Dictionary<string, object> { { "file", fileSystem.Path.GetFileName(absolutePath) }, { "sha256", sha } };
		}

		[Description("Sign a file with the current identity. Writes the signature next to it as <file>.signature. The signature covers the file's name and the sha256 of its bytes. identity is 'default' (the current identity) or the name of another identity of this app")]
		[PLang.Attributes.Effect(PLang.Attributes.Effects.WritesFiles)]
		public async Task<(SignedMessage?, IError?)> SignFile(string path, List<string>? contracts = null, int? expiresInSeconds = null, string identity = "default")
		{
			if (string.IsNullOrEmpty(path)) return (null, new ProgramError("Path cannot be empty.", goalStep));
			var absolutePath = GetPath(path);
			if (!fileSystem.File.Exists(absolutePath)) return (null, new ProgramError($"File not found: {path}", goalStep, Key: "FileNotFound"));

			var data = FileSignatureData(absolutePath);
			// No nonce: a file signature is checked again every time the file is read, a nonce would make the second check fail.
			// Without an expiry VerifySignature calls a signature older than 5 minutes too old, so the default is ten years.
			SignedMessage signature;
			try { signature = await signingService.Sign(data, contracts ?? [FileContract], expiresInSeconds ?? 315360000, null, skipNonce: true, identityName: identity); }
			catch (PLang.Services.IdentityService.IdentityException ex) { return (null, new ProgramError(ex.Message, goalStep, Key: "IdentityNotFound")); }

			// Written under a temporary name and moved, so nobody reads a signature that is half written.
			var signaturePath = absolutePath + SignatureFileExtension;
			var tempPath = signaturePath + ".tmp";
			await fileSystem.File.WriteAllTextAsync(tempPath, JsonConvert.SerializeObject(signature), new System.Text.UTF8Encoding(false));
			fileSystem.File.Move(tempPath, signaturePath, true);
			return (signature, null);
		}

		[Description("Verify a file against its <file>.signature. Returns the signature (its Identity says who signed) when the bytes, the name, the contract and the expiry match; an error otherwise, also when there is no .signature file")]
		public async Task<(SignedMessage?, IError?)> VerifyFile(string path, List<string>? contracts = null)
		{
			if (string.IsNullOrEmpty(path)) return (null, new ProgramError("Path cannot be empty.", goalStep));

			var absolutePath = GetPath(path);
			if (!fileSystem.File.Exists(absolutePath)) return (null, new ProgramError($"File not found: {path}", goalStep, Key: "FileNotFound"));

			var signaturePath = absolutePath + SignatureFileExtension;
			if (!fileSystem.File.Exists(signaturePath)) return (null, new ProgramError($"No signature for {path}: {Path.GetFileName(signaturePath)} is missing.", goalStep, Key: "SignatureMissing"));

			SignedMessage? signature;
			try { signature = JsonConvert.DeserializeObject<SignedMessage>(await fileSystem.File.ReadAllTextAsync(signaturePath)); }
			catch (Exception ex) { return (null, new ProgramError("The signature file could not be read: " + ex.Message, goalStep, Key: "SignatureInvalid")); }
			if (signature == null) return (null, new ProgramError("The signature file is empty.", goalStep, Key: "SignatureInvalid"));
			// VerifySignature only compares the data when the signature carries a hash of it: without one, any file would pass.
			if (signature.Data == null) return (null, new ProgramError("The signature does not cover the file's content (no data hash).", goalStep, Key: "SignatureInvalid"));

			return await signingService.VerifySignature(signature, null, FileSignatureData(absolutePath), contracts ?? [FileContract]);
		}

		public async Task<(string?, IError?)> GetPrivateKey()
		{
			// This should be handled by the AskUserPrivateKeyExport, this Program.cs should not know about it.
			var lockTimeout = settings.GetOrDefault(typeof(AskUserPrivateKeyExport), LockedKey, DateTime.MinValue);
			if (lockTimeout != DateTime.MinValue && lockTimeout > SystemTime.UtcNow().AddDays(-1))
			{
				return (null, new StepError($"System has been locked from exporting private keys. You will be able to export after {lockTimeout}", goalStep));
			}

			var response = settings.GetOrDefault<DecisionResponse>(typeof(AskUserPrivateKeyExport), GetType().Name, new DecisionResponse("none", "", DateTime.MinValue));
			if (response == null || response.Level == "none" || response.Expires < SystemTime.UtcNow())
			{
				var error = new AskUserPrivateKeyExport(llmServiceFactory, settings, GetType().Name);
				return (null, error);
			}

			if (response.Level.ToLower() == "low" || response.Level.ToLower() == "medium")
			{
				var identity = identityService.GetCurrentIdentityWithPrivateKey();
				return (identity.Value.ToString(), null);
			}

			return (null, new StepError(response.Explain, goalStep, "PrivateKeyLocked"));

		}

	}
}
