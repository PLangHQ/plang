using Data = global::app.data.@this;

namespace app.type.item.status;

/// <summary>
/// PLang <c>status</c> value — an outcome's code and its reason: an error's (<c>%!error.status%</c>), an http
/// response's (<c>%response!status%</c>). <c>{code: 404, text: "Not Found"}</c>. Made from a number too: the text is
/// the code's standard reason. It compares with a number by its code, so <c>%!error.status% &gt;= 500</c> holds.
/// </summary>
[global::app.Attributes.PlangType("status")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "object";

    /// <summary>The code — <c>%!error.status.code%</c>.</summary>
    [Out, Store] public global::app.type.item.number.@this Code { get; }

    /// <summary>The reason, in words — the code's standard one unless whoever answered gave its own (an http
    /// server's reason phrase).</summary>
    [Out, Store] public global::app.type.item.text.@this Text { get; }

    /// <summary>Whether it is a success: a code in the 200s.</summary>
    public global::app.type.item.@bool.@this Ok => Code.ToInt32() is >= 200 and < 300;

    /// <summary>The status of <paramref name="code"/>, its text the code's standard reason unless
    /// <paramref name="text"/> gives one.</summary>
    public @this(global::app.type.item.number.@this code, global::app.type.item.text.@this? text = null)
    {
        Code = code;
        Text = text is { } given && given.ToString().Length > 0
            ? given
            : Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(code.ToInt32());
    }

    /// <summary>A status is made from its code.</summary>
    public static implicit operator @this(int code) => new(code);

    public override bool IsLeaf => false;

    /// <summary>Just above number: a number met in a comparison is made a status, and the two compare by code.</summary>
    public override int Rank => 350;

    protected override async System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(
        global::app.type.item.@this other, global::app.actor.context.@this context)
        => Create(other) is { } b
            ? await Code.Compare(b.Code, context)
            : global::app.data.Comparison.Incomparable;

    /// <summary>The pure core: a status passes through; a number is its code.</summary>
    public static @this? Create(object? raw) => raw switch
    {
        @this status => status,
        global::app.type.item.number.@this code => new(code),
        int code => new(code),
        long code => new((int)code),
        _ => null,
    };

    /// <summary>A status is made from its code, or from a dict of its members (<c>{code: 404}</c> — a text left out
    /// is the code's standard reason); anything else declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (Create(raw) is { } made) return made;
        if (raw is global::app.type.item.dict.@this dict)
        {
            global::app.type.item.number.@this? code = null;
            global::app.type.item.text.@this? text = null;
            foreach (var entry in dict.Entries(data.Context!))
                switch (entry.Name.ToLowerInvariant())
                {
                    case "code" when entry.Peek() is global::app.type.item.number.@this c: code = c; break;
                    case "text" when entry.Peek() is global::app.type.item.text.@this t: text = t; break;
                    default:
                        data.Fail(new global::app.error.Error(
                            $"a status's members are code (a number) and text — not {entry.Name}", "StatusInvalid", 400));
                        return null;
                }
            if (code != null) return new(code, text);
        }
        data.Fail(new global::app.error.Error(
            $"a status is a code (404) or {{code, text}} — not a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name}",
            "StatusInvalid", 400));
        return null;
    }

    /// <summary>Writes itself as its code when its text is the code's standard reason (it says nothing more —
    /// <c>404</c>), else as its dict (<c>{code, text}</c>, a server's own reason).</summary>
    public override void Write(global::app.type.format.IWriter writer)
    {
        if (Text.ToString() == Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(Code.ToInt32()))
        {
            Code.Write(writer);
            return;
        }
        writer.BeginObject();
        writer.Name("code"); Code.Write(writer);
        writer.Name("text"); Text.Write(writer);
        writer.EndObject();
    }

    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    public override bool Equals(object? obj) => obj is @this other && Code.Equals(other.Code);
    public override int GetHashCode() => Code.GetHashCode();
}
