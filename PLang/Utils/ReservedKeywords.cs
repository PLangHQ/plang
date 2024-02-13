using System.Reflection;

namespace PLang.Utils
{
	public static class ReservedKeywords
	{
		public static readonly string Identity = "Identity";
		public static readonly string MyIdentity = "MyIdentity";

		public static readonly string Debug = "__Debug__";
		public static readonly string Test = "__Test__";
		public static readonly string Goal = "__Goal__";
		public static readonly string Step = "__Step__";
		public static readonly string StepIndex = "__StepIndex__";

		public static readonly string Instruction = "__Instruction__";
		public static readonly string Event = "__Event__";
		public static readonly string IsEvent = "__IsEvent__";
		public static readonly string MemoryStack = "__MemoryStack__";
		public static readonly string HttpContext = "__HttpContext__";
		public static readonly string IsHttpRequest = "__IsHttpRequest__";
		
		public static readonly string IdentityNotHashed = "__IdentityNotHashed__";
		public static readonly string CurrentDataSourceName = "__CurrentDataSourceName__";
		public static readonly string Exception = "__Exception__";
		public static readonly string LlmDocumentation = "__DOC__";
		public static readonly string Llm = "__LLM__";
		public static readonly string Salt = "__Salt__";

		public static readonly string Inject_IDbConnection = "__Inject_IDbConnection__";
		public static readonly string Inject_SettingsRepository = "__Inject_SettingsRepository__";
		public static readonly string Inject_Logger = "__Inject_Logger__";
		public static readonly string Inject_Caching = "__Inject_Caching__";
		public static readonly string Inject_LLMService = "__Inject_LLMService__";
		public static readonly string Inject_AskUserHandler = "__Inject_AskUserHandler__";
		public static readonly string Inject_EncryptionService = "__Inject_EncryptionService__";
		public static readonly string Inject_IEventSourceRepository = "__Inject_IEventSourceRepository__";
		public static readonly string Inject_Archiving = "__Inject_Archiving__";
		public static readonly string Inject_OutputStream = "__Inject_OutputStream__";


		private static List<string> keywords = new List<string>();
		public static List<string> Keywords
		{
			get
			{
				if (keywords.Count > 0) return keywords;
				FieldInfo[] fields = typeof(ReservedKeywords).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

				foreach (var field in fields)
				{
					if (field.FieldType == typeof(string))
					{
						keywords.Add(field.GetValue(null)!.ToString()!);
					}
				}

				return keywords;
			}
		}


		public static bool IsReserved(string key)
		{
			key = key.Replace("%", "");
			return Keywords.Any(item => item.Equals(key, StringComparison.OrdinalIgnoreCase));
		}

	}
}
