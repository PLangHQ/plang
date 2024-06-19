namespace PLang.Errors
{
	public record ErrorHandled(IError Error) : Error(Error.Message), IErrorHandled;

}
