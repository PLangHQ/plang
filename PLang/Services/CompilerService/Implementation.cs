using PLang.Models;

namespace PLang.Services.CompilerService;

public class Implementation
{
    public Implementation(string @namespace, string name, string code, string[]? @using,
        List<CSharpCompiler.Parameter> inputParameters,
        List<CSharpCompiler.Parameter>? outParameters, GoalToCall? goalToCallOnTrue, GoalToCall? goalToCallOnFalse,
        Dictionary<string, object?>? goalToCallOnTrueParameters = null,
        Dictionary<string, object?>? goalToCallOnFalseParameters = null, List<string>? servicesAssembly = null)
    {
        Namespace = @namespace;
        Name = name;
        Code = code;
        Using = @using;
        InputParameters = inputParameters;
        OutParameters = outParameters;
        GoalToCallOnTrue = goalToCallOnTrue;
        GoalToCallOnFalse = goalToCallOnFalse;
        GoalToCallOnTrueParameters = goalToCallOnTrueParameters;
        GoalToCallOnFalseParameters = goalToCallOnFalseParameters;
        ServicesAssembly = servicesAssembly;
    }

    public string Namespace { get; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string[]? Using { get; private set; }
    public List<CSharpCompiler.Parameter> InputParameters { get; private set; }
    public List<CSharpCompiler.Parameter>? OutParameters { get; private set; }
    public GoalToCall? GoalToCallOnTrue { get; private set; }
    public GoalToCall? GoalToCallOnFalse { get; private set; }
    public Dictionary<string, object?>? GoalToCallOnTrueParameters { get; set; }
    public Dictionary<string, object?>? GoalToCallOnFalseParameters { get; set; }
    public List<string>? ServicesAssembly { get; }
}