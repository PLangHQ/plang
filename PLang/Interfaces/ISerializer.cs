using PLang.Errors;

namespace PLang.Interfaces
{
	public interface ISerializer
	{
		string Type { get; }
		(T, IError) Parse<T>(object obj);
		(object Data, IError Error) Parse(object obj, Type type);
	}
}
