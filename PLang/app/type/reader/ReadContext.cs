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
    // sync-waits (no threadpool starvation under parallel reads). When false, verify runs
    // inline. The peeled Data is born holding its signature layer (Data.Signature); the async
    // caller verifies it and answers a fresh error when it fails.
    bool DeferVerify = false,
    // The variables the row being read holds, as its .pr "variable" list says — each template
    // born under this read takes the ones written in it, so loading never parses. Null outside a
    // .pr row: a template born at build or at run parses itself.
    IReadOnlyList<global::app.type.item.variable.@this>? Variable = null,
    // Where the content being read came from — the file its bytes were read off. A value born under
    // this read (a goal from its .pr) is born holding it, and a refusal names it. Null for content
    // with no location (a channel read, an http body).
    global::app.type.item.path.@this? Origin = null);
