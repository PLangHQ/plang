using Newtonsoft.Json;

namespace PLang.Services.CompilerService
{
	public class CodeImplementationResponse : ImplementationResponse
	{
		public CodeImplementationResponse() { }
		public CodeImplementationResponse(string @namespace, string name, string? implementation = null,
			string[]? inputParameters = null,  string[]? outParameters = null, string[]? @using = null, 
			string[]? assemblies = null)
		{
			Name = name;
			Implementation = implementation;
			Namespace = @namespace;
			InputParameters = inputParameters;
			OutParameters = outParameters;
			Using = @using;
			Assemblies = assemblies;
		}

		public string[]? OutParameters { get; set; } = null;
	}
}
