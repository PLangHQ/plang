using System.Text.Json;

namespace app.start;

/// <summary>
/// What started the app: its command line, read once — the goal it names and the properties it gives. Values are
/// JSON (text when they aren't): strings, numbers, booleans, objects, arrays.
///
/// Examples:
///   plang Start                          → Goal=Start (the goal to run)
///   plang --test module/math             → Goal=module/math (what to test), the flag test=true
///   plang --build                        → the flag build=true
///   plang --debug.setting.goal=Start     → the flag debug.setting.goal="Start"
///   plang name="my app"                  → the value name="my app"
///   plang --build.setting={"files":"test.goal"}  → the flag build.setting={"files":"test.goal"}
///   plang count=42 active=true           → the values count=42, active=true
/// </summary>
public sealed class @this
{
    /// <summary>The one argument that is no property, wherever it stands — the goal to run, or in test mode what to
    /// test; null when none is given.</summary>
    public string? Goal { get; }

    /// <summary>The properties the line gives: its flags (<c>--x</c>) and its values (<c>x=…</c>).</summary>
    public property.list.@this property { get; } = new();

    private @this(string? goal, property.list.@this properties)
    {
        Goal = goal;
        property = properties;
    }

    /// <summary>The start <paramref name="args"/> write: the one argument that is no property is the goal; every
    /// other is a property, a <c>--</c> one a flag. A line it can't read — a second goal, a setting's path written as
    /// the goal, one property written twice in different case — is an AppException carrying ArgumentRefused, naming
    /// the argument and the form that says what was meant.</summary>
    public static @this Parse(string[] args)
    {
        string? goal = null;
        var properties = new property.list.@this();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!IsProperty(arg))
            {
                if (goal != null)
                    throw new global::app.error.AppException(Refused($"'{arg}' is a second goal: the line names one goal, '{goal}'. A value is {arg}=…, a setting --{arg}=…."));
                if (IsSettingPath(arg))
                    throw new global::app.error.AppException(Refused($"'{arg}' is no goal: a setting is written --{arg}=…, a value {arg}=…."));
                goal = arg;
                continue;
            }
            var flag = arg.StartsWith("--");
            if (flag) arg = arg[2..];

            var equals = arg.IndexOf('=');
            // a bare flag (--build) is true
            if (equals < 0)
            {
                properties.Add(new property.@this(arg, flag, true));
                continue;
            }

            var key = arg[..equals];
            var raw = arg[(equals + 1)..];
            // a value starting { or [ runs on until its JSON is whole
            if (raw.StartsWith('{') || raw.StartsWith('[')) raw = Json(raw, args, ref i);
            properties.Add(new property.@this(key, flag, Value(raw)));
        }
        return new(goal, properties);
    }

    /// <summary>What this start's flags set, by the address each is kept at (<c>--test.setting.timeout=5s</c> is the
    /// path <c>test.setting.timeout</c> and its value), every flag checked by the setting it names in
    /// <paramref name="store"/> — but those that are the caller's own <paramref name="words"/> (its mode words), left out.
    /// The first a setting refuses (a path no setting goes by, an option it doesn't have, a value its option can't take)
    /// is the answer. Nothing is written.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this<global::app.type.item.dict.@this>> Values(
        global::app.actor.setting.@this store, global::app.actor.context.@this context, IEnumerable<string>? words = null)
    {
        var values = new global::app.type.item.dict.@this(context.App.type.list[typeof(global::app.type.item.dict.@this)]);
        foreach (var flag in property.Items().Where(p => p.IsFlag && words?.Contains(p.Name, StringComparer.OrdinalIgnoreCase) != true))
        {
            var setting = store.Find(flag.Name);
            if (!setting.Success) return context.Error<global::app.type.item.dict.@this>(setting.Error!);
            var set = (await setting.Value())!.Apply(flag.Name, flag.Value, context);
            if (!set.Success) return set;
            foreach (var entry in (await set.Value())!.Entries(context)) values.Set(entry.Name, entry);
        }
        return context.Ok(values);
    }

    /// <summary>What this start's flags set (<see cref="Values"/>), written into <paramref name="store"/> with its one
    /// Set — all of them, or, on a refusal, none.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this<global::app.type.item.dict.@this>> Set(
        global::app.actor.setting.@this store, global::app.actor.context.@this context, IEnumerable<string>? words = null)
    {
        var values = await Values(store, context, words);
        if (!values.Success) return values;
        foreach (var entry in (await values.Value())!.Entries(context)) store.Set(entry.Name, entry);
        return values;
    }

    // A JSON value that may span several arguments (spaces in it): read on until it parses, moving past what it took.
    private static string Json(string start, string[] args, ref int i)
    {
        if (IsJson(start)) return start;
        var text = new System.Text.StringBuilder(start);
        while (++i < args.Length)
        {
            text.Append(' ').Append(args[i]);
            if (IsJson(text.ToString())) return text.ToString();
        }
        // the end with no whole JSON: what there is, read as text by Value; back one so the loop moves on right
        i--;
        return text.ToString();
    }

    private static bool IsJson(string text)
    {
        try { JsonDocument.Parse(text); return true; }
        catch (JsonException) { return false; }
    }

    private static bool IsProperty(string arg)
        => !string.IsNullOrWhiteSpace(arg) && (arg.StartsWith("--") || arg.Contains('='));

    // A dotted word that is no path (no separator, not ./ or ../, not a .goal): a setting's path missing its --.
    private static bool IsSettingPath(string arg)
        => arg.Contains('.') && !arg.StartsWith('.') && !arg.Contains('/') && !arg.Contains('\\')
           && !arg.EndsWith(".goal", StringComparison.OrdinalIgnoreCase);

    /// <summary>What the line can't be read as, <paramref name="why"/>: the run's answer (ArgumentRefused), whoever
    /// reads the line — the parse, or the run reading its mode words.</summary>
    internal static global::app.error.@this Refused(string why) => new(why, "ArgumentRefused", 400);

    // A value as written: JSON first (numbers, booleans, null, objects, arrays), else the text, its shell quotes off.
    private static object Value(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        if ((raw.StartsWith('"') && raw.EndsWith('"')) || (raw.StartsWith('\'') && raw.EndsWith('\'')))
            raw = raw[1..^1];
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var element = doc.RootElement;
            return element.ValueKind switch
            {
                // the line is read before any app or actor exists, so an object or array is a raw CLR tree, never a
                // plang dict or list — the --debug/--test/--app readers take it as IDictionary<string, object?>
                JsonValueKind.Object => Raw(element)!,
                JsonValueKind.Array => Raw(element)!,
                // a number by number's one rule, as the raw CLR value the line holds
                JsonValueKind.Number => global::app.type.item.number.@this.Parse(element.GetRawText())!.BoxedValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                JsonValueKind.String => element.GetString()!,
                _ => raw
            };
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    // A JSON element as a raw CLR tree (Dictionary/List of raw scalars): no app exists yet to read it into plang values.
    private static object? Raw(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => Raw(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Raw).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => global::app.type.item.number.@this.Parse(element.GetRawText())!.BoxedValue,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
