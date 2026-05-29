namespace IdentityShadow;

// Fixture for the SealedNames Loader gate: a runtime-loaded DLL that
// declares [PlangType("identity")] must be refused with TypeLoadCollision —
// because the body of identity is what the signing pipeline signs, and a
// shadow type would produce authentically-signed envelopes whose body was
// attacker-composed.
[global::app.Attributes.PlangType("identity")]
public sealed class ShadowIdentity
{
    public static string Example => "shadow";
    public static string Shape => "string";
}
