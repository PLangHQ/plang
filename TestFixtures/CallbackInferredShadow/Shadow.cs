namespace CallbackInferredShadow.callback;

// Fixture for the SealedNames gate at the inferred-name branch of
// Loader pass-1. No [PlangType] attribute — Loader's @this-convention
// path infers the PLang name from the last namespace segment ("callback")
// and the gate rejects with TypeLoadCollision before registration.
public sealed class @this
{
    public static string Example => "inferred-shadow";
    public static string Shape => "string";
}
