using System.Text.Json;
using System.Text.Json.Serialization;

namespace app.channel.serializer.json;

/// <summary>
/// The shared STJ read options — the Context-bound converter set (the path
/// adapter <see cref="Converter"/>, string-enums, ISO-8601 TimeSpan,
/// read-numbers-from-string) used to deserialize a JSON / dict graph into CLR
/// records with plang types wired. ONE factory so the file read and a dict's own
/// record reconstruction (<c>dict.Clr</c>) share the exact same converter set —
/// not two copies that can drift. Context-less yields stub Paths; with a Context
/// every <see cref="global::app.type.path.@this"/> field lands scheme-correct.
/// </summary>
public static class Options
{
    public static JsonSerializerOptions Read(global::app.actor.context.@this? context = null)
        => new()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(allowIntegerValues: true),
                new global::app.data.EmptyStringToNullEnumConverterFactory(),
                new global::app.channel.serializer.TimeSpanIso8601(),
                context == null ? new Converter() : new Converter(context),
            },
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
}
