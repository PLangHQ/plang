namespace app.type.item.setting;

/// <summary>
/// A concept's element whose concept has settings of its own names their class: <c>test : IConcept&lt;test.setting&gt;</c>.
/// The concept's type answers them (<c>%!app.test.setting%</c>); an element itself never does, so a test has no
/// <c>.setting</c> of its own. A setting class belongs to what it configures: test's settings configure test runs,
/// so they are test's (goal's list settings — which goals the list lists — stay the list's, <see cref="ISetting{T}"/>).
/// </summary>
public interface IConcept<T> where T : @this, new();
