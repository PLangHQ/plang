using PLang.Building.Model;

namespace PLang.Services.OutputStream
{
	public interface IOutputStreamFactory
	{
		IOutputStream CreateHandler(string? channel = null);
		IOutputStreamFactory SetContext(string? name);
		void SetOutputStream(IOutputStream outputStream);
	}
	public interface IOutputSystemStreamFactory
	{
		IOutputStream CreateHandler(string? channel = null);
		IOutputSystemStreamFactory SetContext(string? name);
	}

}
