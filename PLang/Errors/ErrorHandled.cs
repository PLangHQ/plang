namespace PLang.Errors;

public record ErrorHandled(IError Error) : Error(Error.Message), IErrorHandled
{
    public bool IgnoreError => false;

    public IError? InitialError { get; } = Error;
}