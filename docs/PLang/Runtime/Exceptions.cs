namespace PLang.Runtime;

public class GoalNotFoundException : Exception
{
    public string GoalPath { get; }
    
    public GoalNotFoundException(string path) 
        : base($"Goal not found: {path}")
    {
        GoalPath = path;
    }
}

public class ModuleNotFoundException : Exception
{
    public string ModuleName { get; }
    
    public ModuleNotFoundException(string name)
        : base($"Module not found: {name}")
    {
        ModuleName = name;
    }
}

public class SerializerNotFoundException : Exception
{
    public string SerializerName { get; }
    
    public SerializerNotFoundException(string name)
        : base($"Serializer not found: {name}")
    {
        SerializerName = name;
    }
}

public class StepExecutionException : Exception
{
    public Step Step { get; }
    
    public StepExecutionException(Step step, string message, Exception? inner = null)
        : base(message, inner)
    {
        Step = step;
    }
}
