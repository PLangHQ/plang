namespace PLang.Errors.Builder
{
	public interface IInstructionBuilderError : IBuilderError
	{
		public bool ExcludeModule { get; set; }
	}
}
