namespace PLang.Tests.Shared;

/// <summary>
/// Test helper: birth a <c>Data</c> (and value) FROM the app's user-actor context —
/// the default test context. Tests born-with-context this way instead of the retired
/// context-less <c>Data.Ok(...)</c> / <c>new Data(...) { Context = … }</c> construct-then-stamp
/// pattern (which builds a context-less, mis-typed value — see CLAUDE.md / good_to_know).
///
/// Mirrors the production factories on <c>actor.context.@this</c> — <c>context.Ok/Null/Error/NotFound</c>.
/// The sweep is a token swap: <c>Data.Ok(x)</c> → <c>app.Ok(x)</c>, where <c>app</c> is the
/// test's <c>app.@this</c> local. For a non-user context, call <c>app.System.Context.Ok(x)</c> directly.
/// </summary>
public static class TestDataExtensions
{
    /// <summary>Empty success Data, born from the app's user context.</summary>
    public static global::app.data.@this Ok(this global::app.@this app)
        => app.User.Context.Ok();

    /// <summary>Success Data wrapping <paramref name="value"/>, born from the app's user context.</summary>
    public static global::app.data.@this Ok(this global::app.@this app, object? value,
        global::app.type.@this? type = null)
        => app.User.Context.Ok(value, type);

    /// <summary>Typed success Data, born from the app's user context.</summary>
    public static global::app.data.@this<T> Ok<T>(this global::app.@this app, T value,
        global::app.type.@this? type = null)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
        => app.User.Context.Ok(value, type);

    /// <summary>Present-null Data, born from the app's user context.</summary>
    public static global::app.data.@this Null(this global::app.@this app, string name = "")
        => app.User.Context.Null(name);

    /// <summary>Error Data, born from the app's user context.</summary>
    public static global::app.data.@this Error(this global::app.@this app, global::app.error.IError error)
        => app.User.Context.Error(error);

    /// <summary>Not-found Data, born from the app's user context.</summary>
    public static global::app.data.@this NotFound(this global::app.@this app, string name = "")
        => app.User.Context.NotFound(name);

    /// <summary>A named Data wrapping <paramref name="value"/>, born from the app's user context
    /// (replaces <c>new Data(name, value) { Context = app.User.Context }</c>).</summary>
    public static global::app.data.@this Data(this global::app.@this app, string name, object? value,
        global::app.type.@this? type = null)
        => new(name, value, type, context: app.User.Context);
}
