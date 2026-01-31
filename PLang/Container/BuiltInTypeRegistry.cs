using Microsoft.Data.Sqlite;
using PLang.Interfaces;
using PLang.Services.ArchiveService;
using PLang.Services.CachingService;
using PLang.Services.EncryptionService;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Services.SettingsService;
using System.Data;

namespace PLang.Container
{
	/// <summary>
	/// Registry of built-in service implementations that can be registered via plang inject command.
	/// Maps friendly names to their implementations.
	/// </summary>
	public static class BuiltInTypeRegistry
	{
		// Database implementations
		private static readonly Dictionary<string, Type> DbTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "sqlite", typeof(SqliteConnection) },
			{ "default", typeof(SqliteConnection) },
		};

		// LLM service implementations
		private static readonly Dictionary<string, Type> LlmTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "plang", typeof(PLangLlmService) },
			{ "openai", typeof(OpenAiService) },
			{ "default", typeof(PLangLlmService) },
		};

		// Caching implementations
		private static readonly Dictionary<string, Type> CachingTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "memory", typeof(InMemoryCaching) },
			{ "inmemory", typeof(InMemoryCaching) },
			{ "default", typeof(InMemoryCaching) },
		};

		// Settings repository implementations
		private static readonly Dictionary<string, Type> SettingsTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "sqlite", typeof(SqliteSettingsRepository) },
			{ "default", typeof(SqliteSettingsRepository) },
		};

		// Archiver implementations
		private static readonly Dictionary<string, Type> ArchiverTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "zip", typeof(Zip) },
			{ "default", typeof(Zip) },
		};

		// Encryption implementations
		private static readonly Dictionary<string, Type> EncryptionTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "default", typeof(Encryption) },
			{ "aes", typeof(Encryption) },
		};

		// Logger implementations
		private static readonly Dictionary<string, Type> LoggerTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "default", typeof(Services.LoggerService.Logger<Executor>) },
			{ "console", typeof(Services.LoggerService.Logger<Executor>) },
		};

		/// <summary>
		/// Get the implementation type for a service by its friendly name.
		/// Returns null if not a built-in type (caller should check .services folder).
		/// </summary>
		public static Type? GetBuiltInType(string serviceType, string implementationName)
		{
			var registry = serviceType.ToLower() switch
			{
				"db" => DbTypes,
				"llm" => LlmTypes,
				"caching" => CachingTypes,
				"settings" => SettingsTypes,
				"archiver" => ArchiverTypes,
				"encryption" => EncryptionTypes,
				"logger" => LoggerTypes,
				_ => null
			};

			if (registry == null) return null;

			return registry.TryGetValue(implementationName, out var type) ? type : null;
		}

		/// <summary>
		/// Check if the path looks like a built-in type name (not a file path).
		/// </summary>
		public static bool IsBuiltInTypeName(string pathOrName)
		{
			// If it contains path separators or .dll extension, it's a file path
			if (pathOrName.Contains('/') || pathOrName.Contains('\\') ||
				pathOrName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			// If it's a simple name without dots, likely a built-in name
			// Exception: full type names like "PLang.Services.LlmService.PLangLlmService"
			if (!pathOrName.Contains('.'))
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Get the interface type for a service type name.
		/// </summary>
		public static Type? GetInterfaceType(string serviceType)
		{
			return serviceType.ToLower() switch
			{
				"db" => typeof(IDbConnection),
				"llm" => typeof(ILlmService),
				"caching" => typeof(IAppCache),
				"settings" => typeof(ISettingsRepository),
				"archiver" => typeof(IArchiver),
				"encryption" => typeof(IEncryption),
				"logger" => typeof(ILogger),
				"askuser" => typeof(IAskUserHandler),
				_ => null
			};
		}
	}
}
