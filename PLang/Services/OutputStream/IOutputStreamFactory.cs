namespace PLang.Services.OutputStream
{
	public interface IOutputStreamFactory
	{
		IOutputStream CreateHandler(string? name = null);
		IOutputStreamFactory SetContext(string? name);
	}
	public interface IOutputSystemStreamFactory
	{
		IOutputStream CreateHandler(string? name = null);
		IOutputSystemStreamFactory SetContext(string? name);
	}

}
