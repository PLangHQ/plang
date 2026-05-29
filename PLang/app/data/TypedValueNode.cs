namespace app.data;

/// <summary>
/// Marker node produced by <see cref="@this.Normalize"/> when it encounters a
/// value whose CLR type resolves to a registered <see cref="app.Attributes.PlangTypeAttribute"/>
/// name AND has at least one entry in <see cref="app.types.renderers.@this"/>.
///
/// <para>The marker is format-agnostic: it carries the value and its PLang type
/// name, nothing about how to render. The <see cref="app.channels.serializers.IWriter"/>
/// implementation (which knows its own <see cref="app.channels.serializers.IWriter.Format"/>)
/// resolves the marker against the renderer dispatch and invokes the
/// per-(type, format) <c>Write</c>.</para>
///
/// <para>Adding a new wire format never touches Normalize — the new writer
/// adds one <c>case TypedValueNode</c> and inherits the whole type vocabulary.</para>
/// </summary>
public sealed record TypedValueNode(object Value, string TypeName);
