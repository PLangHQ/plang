using Newtonsoft.Json;
using static PLang.Modules.BaseBuilder;

namespace PLang.Services.CompilerService
{
	public abstract record ImplementationResponse : IGenericFunction
	{

		public string Reasoning { get; set; }
		public string Namespace { get; set; }
		public string Name { get; set; }
		[JsonIgnore] 
		public string Implementation { get; set; }
		public List<string>? Using { get; set; } = null;
		public List<string>? Assemblies { get; set; } = null;
		public List<Parameter>? Parameters { get; set; } = null;
		public List<ReturnValue>? ReturnValues { get; set; }
		public Building.Model.Instruction Instruction { get; set; }
	}
}
