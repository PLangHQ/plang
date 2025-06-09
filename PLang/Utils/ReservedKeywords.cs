namespace PLang.Utils
{
	public static class ReservedKeywords
	{
		public static readonly string Identity = "Identity";
		public static readonly string MyIdentity = "MyIdentity";
		public static readonly string ServiceIdentity = "ServiceIdentity";

		public static readonly string ParametersAtAppStart = "!ArgsAtAppStart";
		public static readonly string Debug = "!Debug";
		public static readonly string CSharpDebug = "!CSharpDebug";
		public static readonly string Test = "!Test";
		public static readonly string Goal = "!Goal";
		public static readonly string Step = "!Step";
		public static readonly string StepIndex = "!StepIndex";
		public static readonly string StrictBuild = "!StrictBuild";
		public static readonly string Instruction = "!Instruction";
		public static readonly string CallingInstruction = "!CallingInstruction";
		public static readonly string Event = "!Event";
		public static readonly string IsEvent = "!IsEvent";
		public static readonly string MemoryStack = "!MemoryStack";
		public static readonly string HttpContext = "!HttpContext";
		public static readonly string IsHttpRequest = "!IsHttpRequest";
		public static readonly string GUID = "!GUID";

		public static readonly string CurrentDataSource = "!CurrentDataSource";
		public static readonly string Error = "!Error";
		public static readonly string DetailedError = "!DetailedError";
		public static readonly string LlmDocumentation = "!DOC";
		public static readonly string Llm = "!LLM";

		public static readonly string Inject_IDbConnection = "!Inject_IDbConnection";
		public static readonly string Inject_SettingsRepository = "!Inject_SettingsRepository";
		public static readonly string Inject_Logger = "!Inject_Logger";
		public static readonly string Inject_Caching = "!Inject_Caching";
		public static readonly string Inject_LLMService = "!Inject_LLMService";
		public static readonly string Inject_AskUserHandler = "!Inject_AskUserHandler";
		public static readonly string Inject_ErrorHandler = "!Inject_ErrorHandler";
		public static readonly string Inject_ErrorSystemHandler = "!Inject_ErrorSystemHandler";
		public static readonly string Inject_EncryptionService = "!Inject_EncryptionService";
		public static readonly string Inject_IEventSourceRepository = "!Inject_IEventSourceRepository";
		public static readonly string Inject_Archiving = "!Inject_Archiving";
		public static readonly string Inject_OutputStream = "!Inject_OutputStream";
		public static readonly string Inject_OutputSystemStream = "!Inject_OutputSystemStream";
		public static readonly string ParentGoalIndent = "!ParentGoalIndent";
		public static readonly string GoalTree = "!GoalTree";
		public  static readonly string VariableValue = "!VariableValue";
		public static readonly string VariableName = "!VariableName";
		public static readonly string OutputTarget = "!OutputTarget";
		public static readonly string DefaultTargetElement = "!TargetElement";
		internal static string StartingEngine = "!StartingEngine";
		internal static string Signature = "!Signature";
		private static List<string> keywords = new List<string>();
		public static List<string> Keywords
		{
			get
			{
				if (keywords.Count > 0) return keywords;
				keywords = TypeHelper.GetStaticFields(typeof(ReservedKeywords));
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
