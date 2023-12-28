using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Utils;

namespace PLang.Modules.LlmModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override async Task<Instruction> Build(GoalStep step)
		{
			return await Build(step, null, 0);
		}

		public async Task<Instruction> Build(GoalStep step, string error = null, int errorCount = 0)
		{
			AppendToAssistantCommand(@"The following user request is for constructing a message to LLM engine

Determine what part is system, assistant and user properties. If you cannot map it, the whole user request should be on user property
llmResponseType can be text, json or html. If user does not define scheme then default to text, if scheme is defined use json

## examples ##
system: do stuff, user: this is data from user, write to %data%, %output% and %dest% => scheme: null, llResponseType=text
system: setup up system, asssistant: some assistant stuff, user: this is data from user, scheme: {data:string, year:number, name:string} => scheme:  {data:string, year:number, name:string}
## examples ##
");
			if (error != null)
			{
				AppendToAssistantCommand(error);
			}
			
			var result = await base.Build(step);
			var genericFunction = result.Action as GenericFunction;
			if (genericFunction != null)
			{
				var scheme = genericFunction.Parameters.FirstOrDefault(p => p.Name == "scheme");
				if (scheme != null && !JsonHelper.LookAsJsonScheme(scheme.Value.ToString()))
				{
					if (errorCount < 2)
					{
						error = $"\nChatGPT generated follow scheme property: {scheme.Value}\n\nThis is not valid json. Can you try to generate a valid json from user request.";
						return await Build(step, error, ++errorCount);
					}

					throw new BuilderStepException($"Could not determine scheme for the step. Make sure to include a json scheme, e.g. {{Result:string}}. Step: {step.Text}", step);
				}
			}
			return result;
		}
		


	}
}

