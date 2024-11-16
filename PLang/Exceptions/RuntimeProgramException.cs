using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Utils;
using static PLang.Modules.BaseBuilder;

namespace PLang.Exceptions;

public class RuntimeProgramException : Exception
{
    public RuntimeProgramException(string message, int statusCode, string type, GoalStep? step) : base(message)
    {
        StatusCode = statusCode;
        Type = type;
        Step = step;
    }

    public RuntimeProgramException(string message, int statusCode, string type, GoalStep step,
        GenericFunction genericFunction, Dictionary<string, object?> parameterValues,
        Exception? ex = null) : base(message, ex)
    {
        Step = step;
        ParameterValues = parameterValues;
        GenericFunction = genericFunction;
        StatusCode = statusCode;
        Type = type;
    }

    public int StatusCode { get; }
    public string Type { get; }
    public GoalStep? Step { get; set; }
    public Dictionary<string, object?>? ParameterValues { get; }
    public GenericFunction? GenericFunction { get; }

    public override string ToString()
    {
        var innerEx = "";
        var ex = InnerException;
        while (ex != null)
        {
            innerEx += $@"
------
Message: {ex.Message}
StackTrace: {ex.StackTrace}
------
";
            ex = ex.InnerException;
        }

        var error = Message;
        AppContext.TryGetSwitch(ReservedKeywords.Debug, out var isDebug);
        if (!isDebug) AppContext.TryGetSwitch(ReservedKeywords.CSharpDebug, out isDebug);

        if (isDebug && Step != null && GenericFunction != null)
            error += $@"

Error happend at
	Step: {Step.Text} line {Step.LineNumber}
	Goal: {Step.Goal.GoalName} in {Step.Goal.GoalFileName}

	Calling {Step.ModuleType}.Program.{GenericFunction.FunctionName} 
		Parameters:
			{JsonConvert.SerializeObject(GenericFunction.Parameters)} 

		Parameter values:
{JsonConvert.SerializeObject(ParameterValues)}

		Return value {JsonConvert.SerializeObject(GenericFunction.ReturnValues)}
	
	------
	Exception: {Message}
	StackTrace: {StackTrace}
	{innerEx}";
        return error;
    }
}