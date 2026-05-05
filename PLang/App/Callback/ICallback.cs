namespace App.Callback;

/// <summary>
/// Marker for callback record types (Stage 4 introduces <c>AskCallback</c> and
/// <c>ErrorCallback</c>). Used by <see cref="Data.@this"/>'s lazy <c>Signature</c>
/// getter to decide whether to seed <c>Expires</c> from <c>app.Callback.Signature.ExpiresInMs</c>
/// — only callback values get the configured timeout; everything else keeps default no-expiry.
/// Stage 4 expands this with <c>Position</c>, <c>Serialize</c>, and <c>Run</c>.
/// </summary>
public interface ICallback
{
}
