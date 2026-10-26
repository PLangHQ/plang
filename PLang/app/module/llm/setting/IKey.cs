namespace app.module.llm.setting;

/// <summary>
/// A setting that holds a service's key (<c>%!llm.setting.key%</c>, <c>%!llm.decider.setting.key%</c>): one held
/// answers at once; none held, the user is asked once, secretly, through the actor's ask, and the answer is saved on
/// the asking actor's row — never asked again. With no one to ask, <see cref="Missing"/>, naming both ways to give one.
/// </summary>
public interface IKey
{
    /// <summary>The key — saved, else its environment variable's. Never shown.</summary>
    global::app.type.item.text.@this Key { get; set; }

    /// <summary>What the user is asked when no key is held.</summary>
    global::app.type.item.text.@this Question { get; }

    /// <summary>Why there is no key when no one gave one — naming both ways to give one.</summary>
    global::app.error.@this Missing { get; }

    /// <summary>The key, as it stands after asking.</summary>
    async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.text.@this>> Asked(global::app.actor.context.@this context)
    {
        if (Key.ToString().Length > 0) return context.Ok(Key);
        var asked = await new global::app.goal.step.action.@this(new global::app.module.output.ask(context)
        {
            Question = Question,
            Sensitive = (global::app.type.item.@bool.@this)true,
        }, context).Start(context);
        // no one answered — no input, or an input with nothing in it — or nothing was given: the ask's own error is
        // the cause
        if (!asked.Success || await asked.Value() is not global::app.type.item.sensitive.@this { Characters.Length: > 0 } sensitive)
        {
            var missing = Missing;
            if (asked.Error is { } error) missing.list.Add(error);
            return context.Error<global::app.type.item.text.@this>(missing);
        }
        Key = sensitive.Characters;
        // only a setting holds a key, so this is one: saved whole on the asker's row
        var row = await context.Setting.Save((global::app.type.item.setting.@this)this);
        return row.Success ? context.Ok(Key) : context.Error<global::app.type.item.text.@this>(row.Error!);
    }
}
