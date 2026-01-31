using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Utils;
using PLang.Utils.Extractors;
using static PLang.Modules.BaseBuilder;
using static PLang.Services.LlmService.PLangLlmService;

namespace PLang.Modules.LlmModule
{
	public class Builder : BaseBuilder
	{
		private readonly IEngine engine;

		public Builder(IEngine engine) : base()
		{
			this.engine = engine;
		}

		public override async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step, IBuilderError? previousBuildError = null)
		{
			AppendToSystemCommand(@"The following user request is for constructing a message to LLM engine

llmResponseType can be null, text, json, markdown or html. default is null. 
When user defines a scheme then use json for llmResponseType, unless user defines otherwise

promptMessages contains the system, assistant and user messages. assistant or user message is required.
Determine what part is system, assistant and user properties. If you cannot map it, the whole user request should be on user role
if user does not define model, set model to ""gpt-4.1-mini"" if content type is image_url
model is default gpt-4.1-mini
the json scheme from promptMessages without image is : {role:string, content:[{type:string, text:string}]} 
the json scheme from promptMessages with image is : {role:string, content:[{type:string, image_url:{url:string}]} 

## examples ##
system: %system%
[
	{
        ""role"": ""system"",
        ""content"": [
            {
                ""type"": ""text"",
                ""text"": ""%system%""
            }
        ]
    }
]


system: %system%, assistant: %assistant%, user: %user%

promptMessages: 
[
	{
        ""role"": ""system"",
        ""content"": [
            {
                ""type"": ""text"",
                ""text"": ""%system%""
            }
        ]
    },
    {
        ""role"": ""assistant"",
        ""content"": [
            {
                ""type"": ""text"",
                ""text"": ""%assistant%""
            }
        ]
    },
    {
        ""role"": ""user"",
        ""content"": [
            {
                ""type"": ""text"",
                ""text"": ""%user%""
            }
        ]
    }
]


system: do stuff, user: this is data from user, write to %data%, %output% and %dest% => scheme: null, llResponseType=null
system: setup up system, asssistant: some assistant stuff, user: this is data from user, scheme: {data:string, year:number, name:string} => scheme:  {data:string, year:number, name:string}

promptMessages: 
[
	{
        ""role"": ""system"",
        ""content"": [
            {
                ""type"": ""text"",
                ""text"": ""setup up system\nYou MUST respond in JSON, scheme:{data:string, year:number, name:string}""
            }
        ]
    },
    {
        ""role"": ""assistant"",
        ""content"": [
            {
                ""type"": ""text"",
                ""text"": ""some assistant stuff""
            }
        ]
    },
    {
        ""role"": ""user"",
        ""content"": [
            {
                ""type"": ""text"",
                ""text"": ""this is data from user""
            }
        ]
    }
]

content can also have the type of image_url, the content of image_url json property can be a URL or a base64 of image.
only ""user"" role can send image

""role"": ""user"",
""content"": [
    {
        ""type"": ""image_url"",
        ""image_url"": {
            ""url"": ""%base64OfImage%""
        }
    }
]

or url 

""role"": ""user"",
""content"": [
   {
        ""type"": ""image_url"",
        ""image_url"": {
            ""url"": ""http://example.org/image.jpg""
        }
    }
]



## examples ##


make sure to return value if user wants to write the result into a variable.
");


			(var instruction, var buildError) = await base.Build(step, previousBuildError);
			if (buildError != null || instruction == null)
			{
				return (null, buildError ?? new StepBuilderError("Could not build step", step));
			}

			var genericFunction = instruction.Function as GenericFunction;
			if (genericFunction != null && genericFunction.Name == "AskLlm")
			{
				var scheme = genericFunction.Parameters.FirstOrDefault(p => p.Name == "scheme");
				var responseTypeParameter = genericFunction.Parameters.FirstOrDefault(p => p.Name == "llmResponseType");
				string responseType = responseTypeParameter?.Value as string;

				if (string.IsNullOrEmpty(responseType))
				{
					string error = $"\nLLM gave empty responseType in last request. Please make sure that you give responseType. If non is defined set it as text";
					return await Build(step, new BuilderError(error));
				}

				if (!VariableHelper.IsVariable(scheme?.Value) && responseType == "json")
				{
					List<LlmMessage> messages = new();
					messages.Add(new LlmMessage("system", "Make the user input into a valid json scheme. ONLY give me scheme, DO not explaing. DO not wrap it"));
					messages.Add(new LlmMessage("user", scheme?.Value.ToString()));

					var (llm, llmError) = engine.Modules.Get<LlmModule.Program>();
					if (llmError != null) return (instruction, new BuilderError(llmError));
					var result = await llm!.AskLlm(messages, llmResponseType: "text", model: "gpt-4o");
					var validScheme = result.Item1.ToString() ?? "";
					if (validScheme.Contains("```json"))
					{
						JsonExtractor jsonExtractor = new JsonExtractor();
						validScheme = jsonExtractor.Extract<string>(validScheme);
					}


					var validateResult = JsonHelper.ValidateJson(validScheme);
					if (validateResult.Error != null) return (instruction, new BuilderError(validateResult.Error));

					if (scheme == null) {
						return (null, new StepBuilderError("scheme cannot be null", step));
					}

					scheme = scheme with { Value = JsonConvert.DeserializeObject(validScheme) };
					
					instruction.Properties.AddOrReplace("ReturnScheme", scheme.Value);
				}

				if (scheme != null && scheme.Value != null && !VariableHelper.IsVariable(scheme.Value) && responseType == "json" && !JsonHelper.LookAsJsonScheme(scheme.Value.ToString()))
				{

					string error = $"\nChatGPT generated follow scheme property: {scheme.Value}\n\nThis is not valid JSON scheme. Can you try to generate a valid json from user request.";
					return await Build(step, new BuilderError(error));

				}

			}
			return (instruction, null);
		}



	}
}

