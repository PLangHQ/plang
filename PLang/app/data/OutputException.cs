namespace app.data;

/// <summary>
/// Hard failure during <see cref="@this.Output"/> (the item-writes-itself wire pass). Its
/// <see cref="global::app.error.AppException.Error"/> names the failure mode by key
/// (<c>OutputSelfReference</c>, <c>OutputGetterThrew</c>, …); the write that catches it answers that Error.
/// </summary>
public sealed class OutputException : global::app.error.AppException
{
    public OutputException(string message, string key, System.Exception? inner = null)
        : base(new global::app.error.Error(message, key, 400), inner) { }
}
