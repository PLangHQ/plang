namespace PLang.Exceptions.Handlers
{
	public interface IExceptionHandler
	{
		Task<bool> Handle(Exception exception, int statusCode, string statusText, string message);
	}
}
