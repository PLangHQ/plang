namespace PLang.Errors.Builder
{
	public interface IBuilderError : IError
	{
		public bool ContinueBuild { get; }
		public bool Retry { get; }
	}
}
