
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors.Builder;
using PLang.Models;
using PLang.Utils;
using System.Collections.Generic;
using static PLang.Modules.MockModule.Program;

namespace PLang.Modules.MockModule;

public class Builder : BaseBuilder
{
	private readonly ITypeHelper typeHelper;
	private readonly IGoalParser goalParser;

	public Builder(ITypeHelper typeHelper, IGoalParser goalParser) : base()
	{
		this.typeHelper = typeHelper;
		this.goalParser = goalParser;
	}
	public record ModuleType(string Name);
	public record MethodName(string Name);
	public record GoalToCallAndParameters(GoalToCallInfo GoalToCall, Dictionary<string, object?>? Parameters = null);
	public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
	{
		var modules = typeHelper.GetRuntimeModules();
		var (module, error) = await this.LlmRequest<ModuleType>(@$"
What <module> is user intengint to map?

<modules>
{string.Join("\n", modules.Where(p => !p.FullName.StartsWith("PLang.Modules.MockModule")).Select(p => p.FullName).ToArray())}
</modules>
", goalStep);

		if (error != null) return (null, error);

		var moduleType = typeHelper.GetRuntimeType(module.Name);
		if (moduleType == null) return (null, new BuilderError($"Could not find {module}"));

		var classDescriptionHelper = new ClassDescriptionHelper();
		(var classDesc, error) = classDescriptionHelper.GetClassDescription(moduleType);
		if (error != null) return (null, error);

		(var method, error) = await this.LlmRequest<MethodName>($@"
What method does the user want to map from this <classDescription>

<classDescription>
{JsonConvert.SerializeObject(classDesc)}
", goalStep);
		if (error != null) return (null, error);

		var methodInfo = classDesc.Methods.FirstOrDefault(p => p.MethodName == method.Name);
		if (methodInfo == null) return (null, new BuilderError($"Method {method} could not be found"));


		

		(var goalToCallAndParams, error) = await this.LlmRequest<GoalToCallAndParameters>(@$"Map user intent.

Complete the MockData object for the MockMethod method

MockData(""{moduleType}"", ""{method.Name}"", Parameters|null, GoalToCall)

Your job is to understand user intent and map GoalToCall which is required, and if user defines any parameters. 
Only map parameters the user defines, do not map any other parameters from the method being mocked.

The method user is mocking has the following information:
{JsonConvert.SerializeObject(methodInfo)}

", goalStep);
		if (error != null) return (null, error);

		(var goalFound, var error2) = GoalHelper.GetGoalPath(goalStep, goalToCallAndParams.GoalToCall, goalParser.GetGoals(), new());
		if (error != null) return (null, new BuilderError(error2) { Retry = false });
		goalToCallAndParams.GoalToCall.Path = goalFound.RelativePrPath;

		var mockData = new MockData(goalToCallAndParams.GoalToCall, moduleType.FullName, method.Name, goalToCallAndParams.Parameters);
		var parameters2 = new List<Parameter>();
		parameters2.Add(new Parameter(typeof(MockData).FullNameNormalized(), "mockData", mockData));
		var gf = new GenericFunction("", "MockMethod", parameters2, null);

		var instruction = new Instruction();
		instruction.Function = gf;
		return (instruction, null);


	}
}

