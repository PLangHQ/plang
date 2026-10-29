namespace app.@event.setting;

/// <summary>Where events run: <c>goal</c> — in the app's own goals (app, the default), or in plang's own goals under
/// system/ too (all). Read when a side runs, so a run that sets it is heard at the next moment.</summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The goals events run in: the app's own (app), or plang's own too (all).</summary>
    [Out, Store] public global::app.type.item.choice.@this<Goal> Goal { get; set; } = setting.Goal.app;
}

/// <summary>The goals events run in.</summary>
[global::app.Attributes.PlangType("event.goal"), global::app.Attributes.Choice]
public enum Goal
{
    app,
    all,
}
