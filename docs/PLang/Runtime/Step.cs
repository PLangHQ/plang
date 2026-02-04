namespace PLang.Runtime;

public partial class Step
{
    public int LineNumber { get; }
    public string Text { get; }
    public string ModuleName { get; }
    public string MethodName { get; }
    
    // CallStack can be set explicitly, or inherited from parent Goal
    private CallStack? _callStack;
    public CallStack? CallStack
    {
        get => _callStack;
        set => _callStack = value;
    }
    
    // Parent goal - set when step is executed within a goal
    public Goal? ParentGoal { get; set; }
    
    // Effective CallStack - own or inherited from parent
    public CallStack? EffectiveCallStack => _callStack ?? ParentGoal?.CallStack;
    
    public event Func<Step, object?, Task>? BeforeExecute;
    public event Func<Step, object?, GoalResult, Task>? AfterExecute;
    
    public Step(int lineNumber, string text, string moduleName, string methodName)
    {
        LineNumber = lineNumber;
        Text = text;
        ModuleName = moduleName;
        MethodName = methodName;
    }
    
    public virtual async Task<GoalResult> Execute(Engine engine, object? data = null)
    {
        if (BeforeExecute != null)
            await BeforeExecute(this, data);
        
        // Track in effective call stack if enabled
        EffectiveCallStack?.Push(this);
        
        GoalResult result;
        
        try
        {
            var module = ModuleRegistry.Get(ModuleName);
            
            // Set context on module before execution
            module.Engine = engine;
            module.Goal = ParentGoal!;
            module.Step = this;
            
            // Module.Execute now returns GoalResult directly
            result = await module.Execute(MethodName, data);
        }
        catch (Exception ex)
        {
            result = GoalResult.Error(ex.Message, ex);
        }
        finally
        {
            EffectiveCallStack?.Pop();
        }
        
        if (AfterExecute != null)
            await AfterExecute(this, data, result);
        
        return result;
    }
}
