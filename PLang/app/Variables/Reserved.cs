namespace app.Variables;

/// <summary>
/// Well-known variable names. <c>!</c>-prefixed entries are PLang infrastructure
/// variables (set by the runtime, read by code); non-prefixed are conventional
/// identity-related names. Strings (so <c>const</c> — values, not state).
/// </summary>
public static class Reserved
{
    public const string Identity = "Identity";
    public const string MyIdentity = "MyIdentity";
    public const string ServiceIdentity = "ServiceIdentity";

    public const string ParametersAtAppStart = "!ArgsAtAppStart";
    public const string Debug = "!Debug";
    public const string CSharpDebug = "!CSharpDebug";
    public const string Environment = "!Environment";
    public const string Test = "!Test";

    public const string Goal = "!Goal";
    public const string Step = "!Step";
    public const string StepIndex = "!StepIndex";
    public const string StrictBuild = "!StrictBuild";
    public const string Instruction = "!Instruction";
    public const string CallingInstruction = "!CallingInstruction";
    public const string Event = "!Event";
    public const string IsEvent = "!IsEvent";
    public const string Variables = "!Variables";
    public const string HttpContext = "!HttpContext";
    public const string IsHttpRequest = "!IsHttpRequest";
    public const string GUID = "!GUID";

    public const string CurrentDataSource = "!CurrentDataSource";
    public const string Error = "!Error";
    public const string DetailedError = "!DetailedError";
    public const string VerboseError = "!VerboseError";
    public const string LlmDocumentation = "!DOC";
    public const string Llm = "!LLM";

    public const string Inject_IDbConnection = "!Inject_IDbConnection";
    public const string Inject_SettingsRepository = "!Inject_SettingsRepository";
    public const string Inject_Logger = "!Inject_Logger";
    public const string Inject_Caching = "!Inject_Caching";
    public const string Inject_LLMService = "!Inject_LLMService";
    public const string Inject_AskUserHandler = "!Inject_AskUserHandler";
    public const string Inject_ErrorHandler = "!Inject_ErrorHandler";
    public const string Inject_ErrorSystemHandler = "!Inject_ErrorSystemHandler";
    public const string Inject_EncryptionService = "!Inject_EncryptionService";
    public const string Inject_IEventSourceRepository = "!Inject_IEventSourceRepository";
    public const string Inject_Archiving = "!Inject_Archiving";
    public const string Inject_OutputStream = "!Inject_OutputStream";
    public const string Inject_OutputSystemStream = "!Inject_OutputSystemStream";
    public const string ParentGoalIndent = "!ParentGoalIndent";
    public const string GoalTree = "!GoalTree";
    public const string VariableValue = "!VariableValue";
    public const string OutputTarget = "!OutputTarget";
    public const string DefaultTargetElement = "!TargetElement";
    internal const string StartingApp = "!StartingApp";
    internal const string Signature = "!Signature";

    /// <summary>
    /// Reflection-derived list of all reserved keys above. Used by <see cref="IsReserved"/>.
    /// Cached on first access — the field set is fixed at compile time.
    /// </summary>
    private static List<string>? _keywords;
    public static List<string> Keywords
    {
        get
        {
            if (_keywords != null) return _keywords;
            _keywords = typeof(Reserved)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                         | System.Reflection.BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(string) && f.IsLiteral)
                .Select(f => (string)f.GetValue(null)!)
                .ToList();
            return _keywords;
        }
    }

    public static bool IsReserved(string key)
    {
        key = key.Replace("%", "");
        return Keywords.Any(item => item.Equals(key, StringComparison.OrdinalIgnoreCase));
    }
}
