using PLang.Errors;

namespace PLang.Modules
{
	public interface IAsyncConstructor
	{
		Task<IError?> AsyncConstructor();
	}
}
