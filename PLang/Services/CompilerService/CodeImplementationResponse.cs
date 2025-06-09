using Newtonsoft.Json;
using static PLang.Modules.BaseBuilder;

namespace PLang.Services.CompilerService
{
	public record CodeImplementationResponse : ImplementationResponse
	{
		public CodeImplementationResponse() { }
		public CodeImplementationResponse(string reasoning, string @namespace, string name, string? implementation = null,
			List<Parameter>? parameters = null, List<ReturnValue>? returnValues = null, List<string>? @using = null, 
			List<string>? assemblies = null)
		{
			Reasoning = reasoning;
			Name = name;
			Implementation = implementation;
			Namespace = @namespace;
			Parameters = parameters;
			ReturnValues = returnValues;
			Using = @using;
			Assemblies = assemblies;
		}

	}
}
