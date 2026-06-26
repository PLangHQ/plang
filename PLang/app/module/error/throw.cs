using app.error;
using app.variable;
using List = global::app.type.list.@this;

namespace app.module.error;

[Action("throw", Cacheable = false)]
public partial class Throw : IContext
{
    /// <summary>
    /// Human-readable message — a quoted string literal: <c>- throw "checkout failed"</c>.
    /// The builder routes a bare literal here; <see cref="Data"/> takes the variables.
    /// </summary>
    public partial data.@this<global::app.type.text.@this>? Message { get; init; }

    /// <summary>
    /// Typed value(s) attached to the error — variables: <c>- throw %order%, %item%</c>.
    /// Stored on the error as a plang <c>list</c> (1..N), navigable via <c>%!error.data%</c>.
    /// A single thrown existing error re-raises intact (Key/Message/StatusCode/chain kept).
    /// </summary>
    public partial data.@this? Data { get; init; }

    [Default(500)]
    public partial data.@this<global::app.type.number.@this> StatusCode { get; init; }
    public partial data.@this<global::app.type.text.@this>? Key { get; init; }

    public async Task<data.@this> Run()
    {
        // An error is a point-in-time capture (like the callstack snapshot), so the
        // attached values bind at throw, not at display.
        global::app.type.item.@this? thrown = Data == null ? null : await Data.Value();

        // Re-raise: `- throw %!error%` hands an existing error straight through rather
        // than wrapping it as a new error's payload. A first-class, intended pattern.
        // TODO: an error isn't a plang type, so it rides as a value inside a clr carrier
        // and the only handle is to open it. When `app.type.error.@this` exists this
        // becomes `thrown is error.@this err → Error(err.Inner)`, no Clr. (todos.md
        // "error as a first-class plang type")
        if (thrown?.Clr<object>() is global::app.error.IError existing)
            return Error(existing);

        // `- throw %!error%` lands the error in the (text) Message slot, not Data. Re-raise
        // it from there too — resolve Message as the apex value (NOT text, which would choke
        // coercing the error object) and hand the existing error straight through.
        if (Message != null && (await Message.Value<global::app.type.item.@this>())?.Clr<object>() is global::app.error.IError msgError)
            return Error(msgError);

        string key = Key == null ? "UserError" : (await Key.Value())?.Clr<string>() ?? "UserError";
        int status = (await StatusCode.Value())!.ToInt32();
        string message = Message == null ? "" : (await Message.Value())?.Clr<string>() ?? "";

        // Normalize the attached values to a list so 1..N is uniform: an already-list
        // value rides as-is, a single value wraps as a list of one. The error never
        // sees a bare value, and never a stringified one.
        global::app.data.@this<List>? attached = null;
        if (Data != null)
        {
            List list = thrown as List ?? new List(new[] { Data });
            attached = Context.Ok<List>(list);
            attached.Context = Context;
        }

        return Error(new ServiceError(message, key, status) { Data = attached });
    }
}
