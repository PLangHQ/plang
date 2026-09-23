namespace app.actor;

/// <summary>
/// The actors a step can name — the closed set a <c>choice&lt;actor&gt;</c> slot draws from. A slot
/// names WHO; the live actor is selected at use through <c>app.Actor[name]</c>. Service is not an
/// actor since the per-call service scopes (<c>app.Services</c>), so it is not in the set.
/// </summary>
[global::app.Attributes.PlangType("actor")]
public enum Name
{
    system,
    user,
}
