using System.Text.Json;

namespace app.type.item.wire.kind.plang;

/// <summary>
/// plang's own writer — <c>application/plang</c>: json, where every Data shows what it is. A record is
/// <c>{"@schema": "data"?, "name"?, "type", "value", "variable"?, "properties"?}</c>: <c>@schema</c> at a layer, the
/// name and the stored variables in the Store view only (a server's binding label is not API surface), the type as
/// plang's kind shows it (its identity). Everything else is json's, written by the json writer it holds.
/// </summary>
public sealed class Writer : global::app.type.format.IWriter
{
    private readonly global::app.type.item.kind.json.Writer _json;

    /// <summary>plang's writer over <paramref name="writer"/>, for <paramref name="view"/>, asked by
    /// <paramref name="context"/>; its kind wire's <c>plang</c> among the asker's types.</summary>
    public Writer(Utf8JsonWriter writer, global::app.View view, global::app.actor.context.@this context)
    {
        _json = new global::app.type.item.kind.json.Writer(writer, view, context);
        View = view;
        Context = context;
        Kind = context.App.type.list["wire"].kind["plang"]!;
    }

    public global::app.type.kind.@this Kind { get; }
    public global::app.View View { get; }
    public global::app.actor.context.@this Context { get; }

    /// <summary>A value written in plang's format: its plang look (characters that already are json pasted), else its
    /// own write.</summary>
    public System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value) => value.Write(this);

    public async System.Threading.Tasks.ValueTask BeginRecord(global::app.data.@this record, bool layer)
    {
        _json.BeginObject();
        // the layer marker (data, beside signature/encryption/compression) — only where a layer begins
        if (layer)
        {
            _json.Name(global::app.data.@this.WireSchema);
            _json.String(global::app.data.@this.WireSchemaData);
        }
        // the name the record shows under, as the view says
        if (View.Name(record) is { } name)
        {
            _json.Name("name");
            _json.String(name);
        }
        if ((record.Error?.Type ?? record.Type) is { IsNull: false } type)
        {
            _json.Name("type");
            await type.Output(this);
        }
        _json.Name("value");
    }

    public async System.Threading.Tasks.ValueTask EndRecord(global::app.data.@this record)
    {
        // a value kept as written names its variables beside it, as the view keeps them
        await View.Variables(record.Peek(), this);
        if (record.Properties.Count > 0)
        {
            _json.Name("properties");
            _json.BeginObject();
            foreach (var kvp in record.Properties)
            {
                _json.Name(kvp.Key);
                if (kvp.Value is global::app.data.@this property)
                    await property.Output(this);
                else
                    await global::app.type.item.@this.Create(kvp.Value, Context).Output(this);
            }
            _json.EndObject();
        }
        // the members a program added to the value, beside it; nothing kept, nothing said
        if (record.Kept.Count > 0)
        {
            _json.Name("kept");
            _json.BeginObject();
            foreach (var (name, member) in record.Kept.Members)
            {
                _json.Name(name);
                await member.Output(this);
            }
            _json.EndObject();
        }
        _json.EndObject();
    }

    public void Null() => _json.Null();
    public void Bool(bool value) => _json.Bool(value);
    public void Int(int value) => _json.Int(value);
    public void Long(long value) => _json.Long(value);
    public void Float(float value) => _json.Float(value);
    public void Double(double value) => _json.Double(value);
    public void String(string value) => _json.String(value);
    public void Raw(string value) => _json.Raw(value);
    public void DateTime(System.DateTime value) => _json.DateTime(value);
    public void DateTimeOffset(System.DateTimeOffset value) => _json.DateTimeOffset(value);
    public void TimeSpan(System.TimeSpan value) => _json.TimeSpan(value);
    public void Guid(System.Guid value) => _json.Guid(value);
    public void Enum(System.Enum value) => _json.Enum(value);
    public void Decimal(decimal value) => _json.Decimal(value);
    public void Bytes(byte[] value, global::app.type.kind.@this kind) => _json.Bytes(value, kind);
    public void BeginArray(int count) => _json.BeginArray(count);
    public void EndArray() => _json.EndArray();
    public void BeginObject() => _json.BeginObject();
    public void Name(string name) => _json.Name(name);
    public void EndObject() => _json.EndObject();
    public void Value(object? normalized) => _json.Value(normalized);
}
