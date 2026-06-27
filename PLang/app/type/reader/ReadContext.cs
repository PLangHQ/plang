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
///
/// <para><see cref="Template"/> is the authored-content mode — <c>"plang"</c> when
/// the bytes are a developer-authored goal/<c>.pr</c> (a <c>%ref%</c> leaf borns a
/// live template), null for every runtime-ingest read (a <c>%ref%</c> borns literal).
/// The trust rides the reader, never the content: a value is templatable only
/// because the reader that read it was constructed in authored mode.</para>
/// </summary>
public sealed record ReadContext(
    global::app.actor.context.@this Context,
    string? Template = null);
