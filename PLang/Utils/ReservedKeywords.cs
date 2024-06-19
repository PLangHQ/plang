﻿using System.Reflection;

namespace PLang.Utils
{
	public static class ReservedKeywords
	{
		public static readonly string Identity = "Identity";
		public static readonly string MyIdentity = "MyIdentity";

		public static readonly string Debug = "!Debug";
		public static readonly string CSharpDebug = "!CSharpDebug";
		public static readonly string Test = "!Test";
		public static readonly string Goal = "!Goal";
		public static readonly string Step = "!Step";
		public static readonly string StepIndex = "!StepIndex";

		public static readonly string Instruction = "!Instruction";
		public static readonly string Event = "!Event";
		public static readonly string IsEvent = "!IsEvent";
		public static readonly string MemoryStack = "!MemoryStack";
		public static readonly string HttpContext = "!HttpContext";
		public static readonly string IsHttpRequest = "!IsHttpRequest";
		
		public static readonly string IdentityNotHashed = "!IdentityNotHashed";
		public static readonly string CurrentDataSource = "!CurrentDataSource";
		public static readonly string Error = "!Error";
		public static readonly string LlmDocumentation = "!DOC";
		public static readonly string Llm = "!LLM";

		public static readonly string Inject_IDbConnection = "!Inject_IDbConnection";
		public static readonly string Inject_SettingsRepository = "!Inject_SettingsRepository";
		public static readonly string Inject_Logger = "!Inject_Logger";
		public static readonly string Inject_Caching = "!Inject_Caching";
		public static readonly string Inject_LLMService = "!Inject_LLMService";
		public static readonly string Inject_AskUserHandler = "!Inject_AskUserHandler";
		public static readonly string Inject_ErrorHandler = "!Inject_ExceptionHandler";
		public static readonly string Inject_EncryptionService = "!Inject_EncryptionService";
		public static readonly string Inject_IEventSourceRepository = "!Inject_IEventSourceRepository";
		public static readonly string Inject_Archiving = "!Inject_Archiving";
		public static readonly string Inject_OutputStream = "!Inject_OutputStream";
		public static readonly string Inject_OutputSystemStream = "!Inject_OutputSystemStream";

		

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
