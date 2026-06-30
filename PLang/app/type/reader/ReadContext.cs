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
    string? Template = null,
    global::app.View View = global::app.View.Out,
    // Verify a signed (@schema:signature) Data on read. The OUTER transport read verifies; a NESTED
    // reconstruction sets false — an inner Data is already covered by the outer signature.
    bool Verify = true,
    // Defer the (async) verify to the async caller instead of running it sync inside the
    // `ref`-struct reader. The plang serializer sets this — it has an async boundary
    // (DeserializeAsync) where it can `await` verify after the sync read, so it never
    // sync-waits (no threadpool starvation under parallel reads). When false (HTTP
    // transport, nested), verify runs inline as before. The reader stamps the unverified
    // signature layer onto the peeled Data via Data.PendingVerification; the async caller
    // verifies and clears it.
    bool DeferVerify = false);
