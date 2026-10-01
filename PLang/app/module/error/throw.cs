using app.error;
using List = global::app.type.item.list.@this;

namespace app.module.error;

[Action("throw", Cacheable = false)]
public partial class Throw : IContext
{
    /// <summary>
    /// Human-readable message — a quoted string literal: <c>- throw "checkout failed"</c>.
    /// The builder routes a bare literal here; <see cref="Data"/> takes the variables.
    /// </summary>
    public partial data.@this<global::app.type.item.text.@this>? Message { get; init; }

    /// <summary>
    /// Typed value(s) attached to the error — variables: <c>- throw %order%, %item%</c>.
    /// Stored on the error as a plang <c>list</c> (1..N), navigable via <c>%!error.data%</c>.
    /// A single thrown existing error re-raises intact (Key/Message/Status/chain kept).
    /// </summary>
    public partial data.@this? Data { get; init; }

    /// <summary>The error's status — <c>throw "x", status 404</c>: made from its code, its text the code's standard
    /// reason. Default: 400.</summary>
    [Default(400)]
    public partial data.@this<global::app.type.item.status.@this> Status { get; init; }

    [Default("error")]
    public partial data.@this<global::app.type.item.text.@this> Key { get; init; }

    /// <summary>How the programmer fixes it — <c>- throw %!error%, fix suggestion %fix%</c>. A re-raised
    /// error keeps its identity and gains the suggestion; a new error is born with it.</summary>
    public partial data.@this<global::app.type.item.text.@this>? FixSuggestion { get; init; }

    public async Task<data.@this> Start()
    {
        // An error is a point-in-time capture (like the callstack snapshot), so the
        // attached values bind at throw, not at display.
        global::app.type.item.@this? thrown = Data == null ? null : await Data.Value();
        // An absent slot is empty, not C# null — asked through IsEmpty, so an error thrown without a
        // fix gets none rather than "".
        string? fix = FixSuggestion == null || await FixSuggestion.IsEmpty() ? null
            : (await FixSuggestion.Value())?.Clr<string>();

        // Re-raise: `- throw %!error%` hands an existing error straight through rather
        // than wrapping it as a new error's payload. A first-class, intended pattern.
        if (thrown is global::app.error.Error existing)
        {
            if (fix != null) existing.FixSuggestion = fix;
            return Error(existing);
        }

        // `- throw %!error%` lands the error in the (text) Message slot, not Data. Re-raise
        // it from there too — resolve Message as the apex value (NOT text, which would choke
        // coercing the error object) and hand the existing error straight through.
        if (Message != null && await Message.Value<global::app.type.item.@this>() is global::app.error.Error msgError)
        {
            if (fix != null) msgError.FixSuggestion = fix;
            return Error(msgError);
        }

        // Key carries its own [Default] — the unset case is answered there, once, where the
        // builder can also read it. A second fallback here would be the default stored twice.
        string key = (await Key.Value())!.Clr<string>()!;
        var status = (await Status.Value())!;
        string message = Message == null ? "" : (await Message.Value())?.Clr<string>() ?? "";

        // Normalize the attached values to a list so 1..N is uniform: an already-list
        // value rides as-is, a single value wraps as a list of one. The error never
        // sees a bare value, and never a stringified one.
        global::app.data.@this<List>? attached = null;
        if (Data != null)
        {
            List list = thrown as List ?? new List(new[] { Data });
            attached = Context.Ok<List>(list);
        }

        return Error(new ServiceError(message, key, status) { Data = attached, FixSuggestion = fix });
    }
}
