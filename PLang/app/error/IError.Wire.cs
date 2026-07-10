using System.Text.Json;
using System.Text.Json.Serialization;
using permission = global::app.type.item.permission.@this;

namespace app.error;

/// <summary>
/// Polymorphic wire converter for <see cref="IError"/> — the shape the Errors
/// snapshot section (a <c>List&lt;IError&gt;</c> trail) rides to disk and back.
///
/// <para>
/// IError has ~10 concrete subclasses but almost none carry their own state:
/// they differ only by default Key / StatusCode / Category. So one flattened
/// shape covers the trail — a <c>$type</c> discriminator plus the common
/// reportable content (id, message, key, statusCode, createdUtc, category,
/// advisory fields, Details, Params, Variables) and the recursive ErrorChain.
/// The two subclasses that DO carry state get their extras: <see cref="AskError"/>
/// (table, dataKey) and <see cref="PermissionDenied"/> (permission).
/// </para>
///
/// <para>
/// Dropped on purpose — the live object graph that can't round-trip: Exception,
/// Step, Goal, CallFrames, Context, App, the lazy Callback. The CallStack
/// snapshot section already carries the frame chain; resume re-enters from there.
/// </para>
///
/// <para>
/// This converter lives ONLY in the snapshot Io options (not the canonical
/// <c>application/plang</c> serializer), so it never changes how errors cross
/// any other wire.
/// </para>
/// </summary>
public sealed class ErrorWire : JsonConverter<IError>
{
    public override bool CanConvert(System.Type typeToConvert)
        => typeof(IError).IsAssignableFrom(typeToConvert);

    public override void Write(Utf8JsonWriter w, IError e, JsonSerializerOptions o)
    {
        w.WriteStartObject();
        w.WriteString("$type", e.GetType().Name);
        w.WriteString("id", e.Id);
        w.WriteString("message", e.Message);
        w.WriteString("key", e.Key);
        w.WriteNumber("statusCode", e.StatusCode);
        w.WriteString("createdUtc", e.CreatedUtc);
        w.WriteString("category", e.Category.ToString());
        if (e.FixSuggestion != null) w.WriteString("fixSuggestion", e.FixSuggestion);
        if (e.HelpfulLinks != null) w.WriteString("helpfulLinks", e.HelpfulLinks);

        if (e is Error baseErr)
        {
            if (baseErr.Details is { Count: > 0 })
            {
                w.WritePropertyName("details");
                JsonSerializer.Serialize(w, baseErr.Details, o);
            }
            if (baseErr.Params is { Count: > 0 })
            {
                w.WritePropertyName("params");
                JsonSerializer.Serialize(w, baseErr.Params, o);
            }
        }

        if (e.Variables is { Count: > 0 })
        {
            w.WritePropertyName("variables");
            JsonSerializer.Serialize(w, e.Variables, o);
        }
        if (e.ErrorChain is { Count: > 0 })
        {
            // Recurses through this same converter for each chained error.
            w.WritePropertyName("errorChain");
            JsonSerializer.Serialize(w, e.ErrorChain, o);
        }

        switch (e)
        {
            case AskError ask:
                w.WriteString("table", ask.Table);
                w.WriteString("dataKey", ask.DataKey);
                break;
            case PermissionDenied pd:
                w.WritePropertyName("permission");
                JsonSerializer.Serialize(w, pd.Permission, o);
                break;
        }

        w.WriteEndObject();
    }

    public override IError Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions o)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var el = doc.RootElement;

        string type        = Str(el, "$type") ?? "Error";
        string id          = Str(el, "id") ?? "";
        string message     = Str(el, "message") ?? "";
        string key         = Str(el, "key") ?? "Error";
        int statusCode     = el.TryGetProperty("statusCode", out var sc) ? sc.GetInt32() : 400;
        DateTime createdUtc = el.TryGetProperty("createdUtc", out var cu) ? cu.GetDateTime() : DateTime.UtcNow;
        string? fix        = Str(el, "fixSuggestion");
        string? links      = Str(el, "helpfulLinks");

        // Reconstruct the concrete type for the two that carry their own state;
        // everything else collapses to the base Error (Key + StatusCode are the
        // only thing that distinguished it), with Id / CreatedUtc preserved.
        Error err = type switch
        {
            nameof(AskError) => new AskError(message,
                Str(el, "table") ?? "",
                Str(el, "dataKey") ?? ""),
            nameof(PermissionDenied) when el.TryGetProperty("permission", out var p) =>
                new PermissionDenied(p.Deserialize<permission>(o)!),
            _ => Error.Restore(id, message, key, statusCode, createdUtc, fix, links),
        };

        if (el.TryGetProperty("details", out var d))
            err.Details = d.Deserialize<Dictionary<string, object?>>(o);
        if (el.TryGetProperty("params", out var pr))
            err.Params = pr.Deserialize<List<ParamSnapshot>>(o);
        if (el.TryGetProperty("variables", out var v))
            err.Variables = v.Deserialize<Dictionary<string, string>>(o) ?? new();
        if (el.TryGetProperty("errorChain", out var chain))
        {
            var restored = chain.Deserialize<List<IError>>(o);
            if (restored != null)
                foreach (var c in restored) err.ErrorChain.Add(c);
        }

        return err;
    }

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}
