using System.Text.Json;

namespace app.data;

/// <summary>
/// Hard failure during <see cref="@this.Output"/> (the item-writes-itself wire pass).
/// <see cref="Key"/> names the failure mode (<c>OutputSelfReference</c>,
/// <c>OutputGetterThrew</c>, …) so callers can map to a typed channel error.
///
/// <para>Subclass of <see cref="JsonException"/> so the serializer's existing
/// <c>catch (Exception ex) when (ex is JsonException …)</c> picks it up and converts
/// it to a <c>PlangSerializeError</c> automatically — the same path every other
/// serialize-time failure already takes.</para>
/// </summary>
public sealed class OutputException : JsonException
{
    public string Key { get; }

    public OutputException(string message, string key, System.Exception? inner = null)
        : base(message, inner) { Key = key; }
}
