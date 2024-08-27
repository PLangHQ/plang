﻿using Newtonsoft.Json;

namespace PLang.Services.CompilerService
{
	public abstract class ImplementationResponse
	{
		public string Namespace { get; set; }
		public string Name { get; set; }
		[JsonIgnore]
		public string Implementation { get; set; }
		public string[]? Using { get; set; } = null;
		public string[]? Assemblies { get; set; } = null;
		public string[]? InputParameters { get; set; } = null;
	}
}
