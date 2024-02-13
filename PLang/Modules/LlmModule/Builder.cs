using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Models;
using PLang.Utils;
using static PLang.Services.LlmService.PLangLlmService;

namespace PLang.Modules.LlmModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override async Task<Instruction> Build(GoalStep step)
		{
			return await Build(step, null, 0);
		}

		public async Task<Instruction> Build(GoalStep step, string? error = null, int errorCount = 0)
		{
			AppendToSystemCommand(@"The following user request is for constructing a message to LLM engine

llmResponseType can be null, text, json, markdown or html. default is null. If scheme is defined then use json, unless user defines otherwise

promptMessages contains the system, assistant and user messages. assistant or user message is required.
Determine what part is system, assistant and user properties. If you cannot map it, the whole user request should be on user role
if user does not define model, set model to ""gpt-4-vision-preview"" if content type is image_url


## examples ##
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
when using base64 append data:XXXX/YYYY;base64, where XXXX/YYYY is the data type, such as image/jpg, image/png, image/gif
""content"": [
    {
        ""type"": ""image_url"",
        ""image_url"": {
            ""url"": ""data:image/jpeg;base64,%base64OfImage%""
        }
    }
]

or url 

""content"": [
   {
        ""type"": ""image_url"",
        ""image_url"": {
            ""url"": ""http://example.org/image.jpg""
        }
    }
]



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
				if (scheme != null && scheme.Value != null && !JsonHelper.LookAsJsonScheme(scheme.Value.ToString()))
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

