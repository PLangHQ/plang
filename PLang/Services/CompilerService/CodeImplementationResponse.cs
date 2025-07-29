using Newtonsoft.Json;
using static PLang.Modules.BaseBuilder;

namespace PLang.Services.CompilerService
{
	public record CodeImplementationResponse : ImplementationResponse
	{
		public CodeImplementationResponse() { }
		public CodeImplementationResponse(string Reasoning, string @Namespace, string Name, string? Implementation = null,
			List<Parameter>? Parameters = null, List<ReturnValue>? ReturnValues = null, List<string>? @Using = null, 
			List<string>? Assemblies = null)
		{
			this.Reasoning = Reasoning;
			this.Name = Name;
			this.Implementation = Implementation;
			this.Namespace = @Namespace;
			this.Parameters = Parameters;
			this.ReturnValues = ReturnValues;
			this.Using = @Using;
			this.Assemblies = Assemblies;
		}

	}
}
