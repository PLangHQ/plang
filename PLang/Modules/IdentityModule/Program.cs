﻿using PLang.Errors.AskUser;
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
		public async Task<(SignedMessage? Signature, IError? Error)> VerifySignature(object? signatureFromUser = null, Dictionary<string, object?>? headers = null, object? body = null, List<string>? contracts = null)
		{
			if (signatureFromUser == null)
			{
				return (null, new ProgramError("Signature is missing"));
			}
			SignedMessage? signature = signatureFromUser as SignedMessage;
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

			return await signingService.VerifySignature(signature, headers, body, contracts);
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
