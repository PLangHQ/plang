using Microsoft.IdentityModel.Tokens;
using Nethereum.ABI;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.LlmService;
using PLang.Services.SigningService;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static Dapper.SqlMapper;
using static PLang.Errors.AskUser.AskUserPrivateKeyExport;

namespace PLang.Modules.CryptographicModule
{
	[Description("Encrypt, descryption and hashing & verify using bcrypt, compute Sha256Hash, generate & validate Bearer token")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly string CurrentBearerToken = "PLang.Modules.CryptographicModule.CurrentBearerToken";
		private readonly IEncryption encryption;
		private readonly ModuleSettings moduleSettings;
		private readonly ISettings settings;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IPLangFileSystem fileSystem;

		public Program(ISettings settings, IEncryptionFactory encryptionFactory,
				ILlmServiceFactory llmServiceFactory, IPLangFileSystem fileSystem) : base()
		{
			this.encryption = encryptionFactory.CreateHandler();
			this.moduleSettings = new ModuleSettings(settings);
			this.settings = settings;
			this.llmServiceFactory = llmServiceFactory;
			this.fileSystem = fileSystem;
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
				return (encryption.GetPrivateKey(), null);
			}

			return (null, new StepError(response.Explain, goalStep, "PrivateKeyLocked"));

		}
		public async Task<string> GetPrivateKeyHash()
		{
			return encryption.GetKeyHash();
		}

		public async Task AddPrivateKey(string privateKey)
		{
			encryption.AddPrivateKey(privateKey);
		}



		public async Task<string> Encrypt(object content)
		{
			return encryption.Encrypt(content);
		}

		public async Task<object> Decrypt(string content)
		{
			return encryption.Decrypt<object>(content);
		}

		public async Task<string?> ConvertToBase64(string content)
		{
			if (string.IsNullOrEmpty(content)) return null;

			byte[] bytes = Encoding.UTF8.GetBytes(content);
			return Convert.ToBase64String(bytes);
		}

		public async Task<byte[]?> ConvertFromBase64(string base64)
		{
			if (string.IsNullOrEmpty(base64)) return null;

			return Convert.FromBase64String(base64);
		}

		public async Task<string> CreateSalt(int workFactor = 12)
		{
			return BCrypt.Net.BCrypt.GenerateSalt(workFactor);
		}

		[Description("Hmac hash sizes are 256, 384, 512")]
		public async Task<string> HashHmacShaInput(string input, string? secretKey = null, int hashSize = 256)
		{
			if (secretKey == null) secretKey = settings.GetSalt();
			if (string.IsNullOrEmpty(secretKey))
			{
				throw new Exception("secretKey is missing");
			}

			return input.ComputeHmacSha256(secretKey, hashSize);
		}

		[Description("Used for hashing. hashAlgorithm: keccak256 | sha256 | bcrypt")]
		public async Task<(string Hash, IError? Error)> HashInput(string input, bool useSalt = true, string? salt = null, string hashAlgorithm = "keccak256")
		{
			if (hashAlgorithm == "bcrypt")
			{
				if (salt == null)
				{
					salt = BCrypt.Net.BCrypt.GenerateSalt();
				}
				return (BCrypt.Net.BCrypt.HashPassword(input, salt), null);
			}
			else
			{
				if (useSalt && salt == null)
				{
					salt = settings.GetSalt();
				}

				return input.ComputeHash(hashAlgorithm, salt);
			}
		}

		[Description("Hashes Identity string to standard hash")]
		public async Task<(string, IError?)> HashIdentityString(string identity)
		{
			if (identity == null)
			{
				throw new RuntimeStepException("identity is null. It cannot be null for hashing", goalStep);
			}
			return identity.ComputeHash(mode: "keccak256", salt: settings.GetSalt());
		}

		[Description("Used to verify hash. hashAlgorithm: keccak256 | sha256 | bcrypt")]
		public async Task<bool> VerifyHashedValues(string text, string hash, string hashAlgorithm = "keccak256", bool useSalt = true, string? salt = null)
		{
			if (text == null)
			{
				throw new RuntimeStepException("text is null. It cannot be null for verifying hash", goalStep);
			}

			if (hashAlgorithm == "bcrypt")
			{
				return BCrypt.Net.BCrypt.Verify(text, hash);
			}
			else
			{
				if (useSalt && salt == null)
				{
					salt = settings.GetSalt();
				}
				return text.ComputeHash(hashAlgorithm, salt).Hash.Equals(hash);
			}
		}

		[Description("Used to verify hash comparing file and a hash. hashAlgorithm: md5 | sha1 | sha256 | sha512. encoding: base64|hex")]
		public async Task<(bool, IError?)> VerifyHashOfFile(string filePath, string expectedHash, string hashAlgorithm = "sha256", string encoding = "base64")
		{
			var absolutePath = GetPath(filePath);
			if (!fileSystem.File.Exists(absolutePath))
			{
				return (false, new ProgramError($"File {filePath} could not be found", goalStep, function, FixSuggestion: $"Make sure that the file {filePath} exists. The absolute path to it is {absolutePath}"));
			}

			var fileBytes = await File.ReadAllBytesAsync(absolutePath);
			var hashAlgo = EncryptionHelper.GetCryptoStandard(hashAlgorithm, expectedHash);
			var fileHashBytes = hashAlgo.ComputeHash(fileBytes);
			string fileHash = "";
			if (encoding == "hex")
			{
				fileHash = BitConverter.ToString(fileHashBytes);
			}
			else
			{
				fileHash = Convert.ToBase64String(fileHashBytes);
			}

			int idx = expectedHash.IndexOf("-");
			if (idx != -1)
			{
				expectedHash = expectedHash.Substring(idx + 1);
			}

			return (fileHash.Equals(expectedHash.ToLowerInvariant()), null);

		}

		public async Task<(string?, IError?)> GetHashOfFile(string filePath, string hashAlgorithm = "sha256", string encoding = "base64")
		{
			var absolutePath = GetPath(filePath);
			if (!fileSystem.File.Exists(absolutePath))
			{
				return (null, new ProgramError($"File {filePath} could not be found", goalStep, function, FixSuggestion: $"Make sure that the file {filePath} exists. The absolute path to it is {absolutePath}"));
			}

			var fileBytes = await File.ReadAllBytesAsync(absolutePath);
			var hashAlgo = EncryptionHelper.GetCryptoStandard(hashAlgorithm);
			var fileHashBytes = hashAlgo.ComputeHash(fileBytes);
			string fileHash = "";
			if (encoding == "hex")
			{
				fileHash = BitConverter.ToString(fileHashBytes);
			}
			else
			{
				fileHash = Convert.ToBase64String(fileHashBytes);
			}
			return (fileHash, null);


		}

		public void Dispose()
		{
			context.Remove(CurrentBearerToken);
		}

		public async Task SetCurrentBearerToken(string name)
		{
			context.AddOrReplace(CurrentBearerToken, name);
		}

		public async Task<string> GetBearerSecret()
		{
			if (context.ContainsKey(CurrentBearerToken))
			{
				return moduleSettings.GetBearerSecret(context[CurrentBearerToken].ToString()).Secret;
			}
			return moduleSettings.GetDefaultBearerSecret().Secret;
		}

		public async Task<bool> ValidateBearerToken(string token, string issuer = "PLangRuntime", string audience = "user")
		{
			try
			{
				var bearerSecret = GetBearerSecret();
				var tokenHandler = new JwtSecurityTokenHandler();
				var validationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidIssuer = issuer,
					ValidAudience = audience,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetBearerSecret().Result))
				};

				// Validate token
				ClaimsPrincipal claimsPrincipal = tokenHandler.ValidateToken(token, validationParameters, out _);

				// Token is valid
				return true;
			}
			catch (Exception ex)
			{
				// Token validation failed
				return false;
			}
		}

		



		public async Task<string> GenerateBearerToken(string uniqueString, string issuer = "PLangRuntime", string audience = "user", int expireTimeInSeconds = 7 * 24 * 60 * 60)
		{
			var bearerSecret = GetBearerSecret().Result;
			var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(bearerSecret));
			var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
			var claims = new[]
			{
				new Claim(JwtRegisteredClaimNames.Sub, uniqueString),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
			};

			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				expires: DateTime.UtcNow.AddSeconds(expireTimeInSeconds),
				signingCredentials: credentials
			);

			// Serialize token to a string
			var bearerToken = new JwtSecurityTokenHandler().WriteToken(token);

			return bearerToken;
		}
	}
}
