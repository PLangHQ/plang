namespace SignatureRendererShadow;

// Fixture for the SealedNames gate at the ITypeRenderer registration site
// (Loader pass-2). No [PlangType] in this assembly — pass-1 has nothing to
// reject, so Loader proceeds to pass-2 where the renderer's TypeName
// ("signature") triggers the SealedNames refusal with TypeLoadCollision.
//
// Uses "signature" (not "identity") to avoid colliding with the
// IdentityShadow fixture's PlangType.
public sealed class ShadowSignatureRenderer : global::app.type.list.ITypeRenderer
{
    public string TypeName => "signature";
    public string Format => global::app.type.renderer.@this.AnyFormat;
    public void Write(object value, global::app.channel.serializer.IWriter writer)
        => writer.String("[shadow-signature]");
}
