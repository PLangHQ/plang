namespace app.type.reader;

/// <summary>
/// The context a <see cref="@this.Read"/> delegate receives when materializing
/// a value from its raw source form. Carries the actor <see cref="Context"/> —
/// a path reads toward a scheme through the Actor's registry, a number reads
/// toward its kind, etc. — and leaves room to grow (a target CLR hint, the
/// source channel) without re-threading every type's <c>Read</c> signature.
///
/// <para>The read-side mirror of the write side's <c>IWriter</c>: where the
/// writer carries the format encoder, the reader carries the decode context.</para>
/// </summary>
public sealed record ReadContext(global::app.actor.context.@this? Context);
