using System.Text;
using System.Text.Json;
using app.channel.serializer;

namespace PLang.Tests.App.Serialization;

// Stage 2b helper — encapsulates the Normalize → JsonWriter → bytes pipeline
// the integration cuts exercise. Mirrors the call shape Wire
// adopts when Stage 2b lands; turning the wiring on later replaces the
// helper's body without touching the test surface.

internal static class NormalizePipelineHelper
{
    public static string SerializeValueSlot(object? rawValue, global::app.View mode = global::app.View.Out)
    {
        var ms = new MemoryStream();
        using (var jw = new Utf8JsonWriter(ms))
        {
            var writer = new global::app.channel.serializer.json.Writer(jw);
            var carrier = new app.data.@this("", rawValue, context: global::PLang.Tests.TestApp.SharedContext);
            var normalized = carrier.Normalize(mode);
            writer.Value(normalized);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Full envelope: emits the canonical Data record shape (name, type, value,
    /// signature) where the value slot routes through Normalize + JsonWriter.
    /// </summary>
    public static string SerializeRecord(app.data.@this record, global::app.View mode = global::app.View.Out)
    {
        var ms = new MemoryStream();
        using (var jw = new Utf8JsonWriter(ms))
        {
            var writer = new global::app.channel.serializer.json.Writer(jw);
            writer.BeginRecord(record);
            var normalized = record.Normalize(mode);
            writer.Value(normalized);
            writer.EndRecord(record);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
