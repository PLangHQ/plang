namespace PLang.Runtime2.modules;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ActionAttribute : Attribute
{
    public string? Name { get; }

    public ActionAttribute() { }
    public ActionAttribute(string name) => Name = name;
}
