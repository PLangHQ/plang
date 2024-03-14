using Newtonsoft.Json;

namespace PLang.Services.CompilerService
{
	public class CodeImplementationResponse : ImplementationResponse
	{
		public CodeImplementationResponse() { }
		public CodeImplementationResponse(string @namespace, string name, string? implementation = null, Dictionary<string, ParameterType>? outParameterDefinition = null, string[]? @using = null, string[]? assemblies = null)
		{
			Name = name;
			Implementation = implementation;
			Namespace = @namespace;
			OutParameterDefinition = outParameterDefinition;
			Using = @using;
			Assemblies = assemblies;
		}

		public Dictionary<string, ParameterType>? OutParameterDefinition { get; set; } = null;
	}
}
