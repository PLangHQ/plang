namespace app.Channels.Serializers;

/// <summary>
/// Hard error raised by <see cref="@this.GetByMimeType"/> when the requested mimetype is
/// not registered. Sibling-shape to <c>ProviderRestoreException</c>: referent-integrity
/// violation, no silent fallback. Channels look up serializers by the receiver's accept/
/// content-type and depend on the registry to be authoritative.
/// </summary>
public sealed class UnregisteredMimeType : System.Exception
{
    public string MimeType { get; }
    public UnregisteredMimeType(string mimeType)
        : base($"No serializer registered for mimetype '{mimeType}'.") { MimeType = mimeType; }
}
