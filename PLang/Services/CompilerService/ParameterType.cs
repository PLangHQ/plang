namespace PLang.Services.CompilerService
{
	public class ParameterType
	{
		public string Name { get; set; }
		public string FullTypeName { get; set; }
	}

	public class ComplexParameterType
	{
		public string Name { get; set; }
		public string FullTypeName { get; set; }
		public List<ComplexParameterType> Properties = new();
	}
}
