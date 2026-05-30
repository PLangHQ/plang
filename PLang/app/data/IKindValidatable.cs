namespace app.data;

/// <summary>
/// A type that can validate its <c>Kind</c> against actual content — used by
/// strict kind enforcement to decide whether a build-time mismatch is a real
/// error.
///
/// <para><c>image</c> implements this and sniffs magic bytes to answer "are
/// these bytes really a GIF?". <c>text</c> does not — there is no probe that
/// can distinguish plain text from markdown by content, so strict on text
/// degrades to "kind name accepted".</para>
///
/// <para>Sibling to <see cref="IBooleanResolvable"/>. The marker lives next to
/// <c>Data</c> (the dispatcher) rather than on the value's own type so
/// strict's validator depends on the marker, not on any concrete value type.</para>
/// </summary>
public interface IKindValidatable
{
    /// <summary>
    /// Validates that <paramref name="value"/> actually is the
    /// <paramref name="requiredKind"/>. Returns <c>(true, null)</c> on match,
    /// <c>(false, actualKind)</c> on mismatch — where <c>actualKind</c> is the
    /// sniffed-from-content kind (or <c>null</c> if undetectable). The validator
    /// is responsible for any byte-sniffing; the caller is shape-only.
    /// </summary>
    (bool ok, string? actualKind) ValidateKind(object value, string requiredKind);
}
