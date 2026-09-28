namespace app.goal.step.unwritten;

/// <summary>
/// A number a step's code writes that its words don't write as digits — <c>RetryCount=1</c> on
/// <c>on error retry once</c>. The words may still give it, in any language; whether they do is the decider's
/// to say (the builder's ConfirmNumbers asks it, one yes/no per number, under <see cref="Id"/>). The check that
/// finds it reads no human language.
/// </summary>
/// <param name="Step">The step's index.</param>
/// <param name="Action">The action writing it, <c>module.action</c>.</param>
/// <param name="Property">The property it is a value of.</param>
/// <param name="Value">The number, as the code writes it.</param>
/// <param name="Text">The step as written.</param>
public sealed record @this(int Step, string Action, string Property, string Value, string Text)
{
    /// <summary>The decider question's id: <c>s&lt;step&gt;_&lt;module.action&gt;.&lt;Property&gt;=&lt;value&gt;</c>.</summary>
    public string Id => $"s{Step}_{Action}.{Property}={Value}";

    /// <summary>Why a number the words don't give is refused.</summary>
    public string Message => $"your answer writes {Value}, which the step doesn't — leave out what the step doesn't give";

    /// <summary>Whether the decider's <paramref name="answer"/> says the step's words give this number. The id holds
    /// dots, so the entry is found by its name, not navigated as a path.</summary>
    public async System.Threading.Tasks.Task<bool> Given(global::app.type.item.dict.@this answer, global::app.actor.context.@this context)
    {
        var entry = answer.Entries(context).FirstOrDefault(e => e.Name == Id);
        return entry != null && await entry.Value() is global::app.type.item.dict.@this a
            && a.Get<global::app.type.item.number.@this>("noul", context) is { } noul
            && noul >= (global::app.type.item.number.@this)0.5;
    }
}
