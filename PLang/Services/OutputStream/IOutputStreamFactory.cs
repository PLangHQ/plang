using PLang.Building.Model;
using PLang.Runtime;

namespace PLang.Services.OutputStream
{
	public interface IOutputStreamFactory
	{
		IOutputStream CreateHandler(string? channel = null);
		IOutputStreamFactory SetContext(string? name);
		void SetEngine(IEngine engine);
	}
	public interface IOutputSystemStreamFactory
	{
		IOutputStream CreateHandler(string? channel = null);
		IOutputSystemStreamFactory SetContext(string? name);
	}

}
