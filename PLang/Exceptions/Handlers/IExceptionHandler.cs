namespace PLang.Exceptions.Handlers
{
	public interface IExceptionHandler
	{
		Task Handle(Exception exception, int statusCode, string statusText, string message);
	}
}
