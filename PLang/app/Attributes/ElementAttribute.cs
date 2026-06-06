namespace app.Attributes;

/// <summary>
/// Declares the element type of a native-collection slot (<c>Data&lt;list&gt;</c> /
/// <c>Data&lt;dict&gt;</c>). A born-native list is element-agnostic at runtime (it holds
/// <c>Data</c> elements), so the CLR type no longer carries the element type the builder
/// needs to teach the LLM. This attribute restores that: the catalog walks the declared
/// element type for its schema and renders the slot as <c>list&lt;element&gt;</c>.
///
/// Runtime resolution is unchanged — the handler reads the typed shape via
/// <c>action.X.GetValue&lt;List&lt;Element&gt;&gt;()</c>; this attribute is a build-time
/// teaching hint only.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
public sealed class ElementAttribute : System.Attribute
{
    public System.Type Element { get; }
    public ElementAttribute(System.Type element) => Element = element;
}
