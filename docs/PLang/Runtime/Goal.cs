namespace PLang.Runtime;

public partial class Goal
{
    public string Path { get; }
    public IReadOnlyList<Step> Steps { get; }
    
    // CallStack - when enabled, steps inherit it
    private CallStack? _callStack;
    public CallStack? CallStack
    {
        get => _callStack;
        set
        {
            _callStack = value;
            // Steps inherit unless they have their own
            foreach (var step in Steps)
            {
                step.ParentGoal = this;
            }
        }
    }
    
    public void EnableCallStack()
    {
        CallStack = new CallStack();
    }
    
    public event Func<Goal, object?, Task>? BeforeRun;
    public event Func<Goal, object?, GoalResult, Task>? AfterRun;
    
    public Goal(string path, IReadOnlyList<Step> steps)
    {
        Path = path;
        Steps = steps;
        
        // Set parent reference on all steps
        foreach (var step in steps)
        {
            step.ParentGoal = this;
        }
    }
    
    public virtual async Task<GoalResult> Run(Engine engine, object? parameters = null)
    {
        if (BeforeRun != null)
            await BeforeRun(this, parameters);
        
        CallStack?.Push(this);
        
        object? lastResult = null;
        
        try
        {
            foreach (var step in Steps)
            {
                var result = await step.Execute(engine, parameters);
                
                if (result.IsError)
                {
                    if (AfterRun != null)
                        await AfterRun(this, parameters, result);
                    return result;
                }
                
                lastResult = result.Data;
            }
        }
        finally
        {
            CallStack?.Pop();
        }
        
        var successResult = GoalResult.Success(lastResult);
        
        if (AfterRun != null)
            await AfterRun(this, parameters, successResult);
        
        return successResult;
    }
}
