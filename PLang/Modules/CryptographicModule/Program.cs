using Microsoft.IdentityModel.Tokens;
using PLang.Interfaces;
using PLang.Utils;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PLang.Modules.CryptographicModule
{
	[Description("Encrypt, descryption and hashing & verify using bcrypt, compute Sha256Hash, generate & validate Bearer token")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly string CurrentBearerToken = "PLang.Modules.CryptographicModule.CurrentBearerToken";
		private readonly PLangAppContext context;
		private readonly IEncryption encryption;
		private readonly ModuleSettings moduleSettings;

		public Program(ISettings settings, PLangAppContext context, IEncryption encryption) : base()
		{
			this.context = context;
			this.encryption = encryption;
			this.moduleSettings = new ModuleSettings(settings);
		}

		public async Task<string> Encrypt(object content)
		{
			return encryption.Encrypt(content);
		}

		public async Task<object> Decrypt(string content)
		{
			return encryption.Decrypt<object>(content);
		}
		public async Task<string> CreateSalt(int workFactor = 12)
		{
			return BCrypt.Net.BCrypt.GenerateSalt(workFactor);
		}

		[Description("Hmac hash sizes are 256, 384, 512")]
		public async Task<string> HashHmacShaInput(string input, string secretKey, int hashSize = 256)
		{
			return input.ComputeHmacSha256(secretKey);
		}

		[Description("Used for hashing, by default it uses salt. Only provide salt if user provides it in his statement. hashAlgorithm: keccak256 | sha256 | bcrypt(when salt)")]
		public async Task<string> HashInput(string input, bool useSalt = false, string? salt = null, string hashAlgorithm = "keccak256")
		{
			if (useSalt)
			{
				if (salt == null)
				{
					salt = BCrypt.Net.BCrypt.GenerateSalt();
				}
				return BCrypt.Net.BCrypt.HashPassword(input, salt);
			} else
			{
				return input.ComputeHash(hashAlgorithm);
			}
		}

		[Description("Used to verify hash, by default it uses salt. hashAlgorithm: keccak256 | sha256 | bcrypt(when salt)")]
		public async Task<bool> VerifyHashedValues(string password, string passwordHash, string hashAlgorithm = "keccak256", bool useSalt = true)
		{
			if (useSalt)
			{
				return BCrypt.Net.BCrypt.Verify(password, passwordHash);
			} else
			{
				return (password.ComputeHash(hashAlgorithm) == passwordHash);
			}
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
