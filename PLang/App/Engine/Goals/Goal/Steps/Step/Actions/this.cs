namespace App.Engine.Goals.Goal.Steps.Step.Actions;

public sealed class @this : List<Action.@this>
{
    public @this() { }
    public @this(IEnumerable<Action.@this> actions) : base(actions) { }

    [System.Text.Json.Serialization.JsonIgnore]
    public Step.@this? Step { get; set; }

    public new Action.@this this[int index]
    {
        get { var a = base[index]; a.Step ??= Step; return a; }
        set => base[index] = value;
    }

    public new IEnumerator<Action.@this> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return this[i];
    }

    public List<Action.@this> Value => this;
}
