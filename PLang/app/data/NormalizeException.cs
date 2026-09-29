namespace app.data;

/// <summary>
/// Hard failure during <see cref="@this.Normalize"/>. Its <see cref="global::app.error.AppException.Error"/>
/// names the failure mode by key (<c>NormalizeCycleDetected</c>, <c>NormalizeMaxDepthExceeded</c>,
/// <c>NormalizeGetterThrew</c>, …); the write that catches it answers that Error.
/// </summary>
public sealed class NormalizeException : global::app.error.AppException
{
    public NormalizeException(string message, string key, System.Exception? inner = null)
        : base(new global::app.error.Error(message, key, 400), inner) { }
}
