namespace SignatureRendererShadow;

// Fixture for the sealed-name gate at the ITypeRenderer registration site. No
// plang type in this assembly, so the load reaches its renderers, where the
// renderer's TypeName ("signature") is refused with TypeLoadCollision.
//
// Uses "signature" (not "identity") to avoid colliding with the
// IdentityShadow fixture's PlangType.
public sealed class ShadowSignatureRenderer : global::app.type.list.ITypeRenderer
{
    public string TypeName => "signature";
    public string Format => global::app.type.renderer.@this.AnyFormat;
    public void Write(object value, global::app.type.format.IWriter writer)
        => writer.String("[shadow-signature]");
}
