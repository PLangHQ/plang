using System.ComponentModel;

namespace PLang.Modules.InjectModule;

[Description("Dependancy injection")]
public class Program : BaseProgram
{
    [Description(
        "type can be: db, settings, caching, logger, llm, askuser, encryption, archiver. Injection can be for runtime, builder or both.")]
    public async Task Inject(string type, string pathToDll, bool isDefaultOrGlobalForWholeApp = false,
        string? environmentVariable = "PLANG_ENV", string? environmentVariableValue = null)
    {
        RegisterForPLangUserInjections(type, pathToDll, isDefaultOrGlobalForWholeApp, environmentVariable,
            environmentVariableValue);
    }
}