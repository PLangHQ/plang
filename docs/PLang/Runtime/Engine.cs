using System.Text.Json;

namespace PLang.Runtime;

public partial class Engine
{
    public PLangAppContext System { get; }
    public PLangContext User { get; }
    
    public IO Out { get; }
    public IO In { get; }
    
    public Goals Goals { get; }
    public CallStack CallStack { get; }
    public EventCollection Events { get; }
    public MemoryStack MemoryStack { get; }
    public SerializerRegistry Serializers { get; }
    
    public Engine()
    {
        System = new PLangAppContext();
        User = new PLangContext();
        Serializers = new SerializerRegistry();
        Out = new IO(Serializers);
        In = new IO(Serializers);
        Goals = new Goals();
        CallStack = new CallStack();
        Events = new EventCollection();
        MemoryStack = new MemoryStack(Events);
    }
    
    public Task<GoalResult> Run(string path, object? parameters = null)
    {
        var goal = Goals.FirstOrDefault(p => p.Path == path);
        if (goal == null)
            return Task.FromResult(GoalResult.Error($"Goal not found: {path}"));
        
        return RunGoal(goal, parameters);
    }
    
    private async Task<GoalResult> RunGoal(Goal goal, object? parameters)
    {
        CallStack.Push(goal);
        
        try
        {
            foreach (var evt in Events.GetBefore(goal))
            {
                if (evt.IsAsync)
                    _ = evt.Handler(goal);
                else
                    await evt.Handler(goal);
            }
            
            var result = await goal.Run(this, parameters);
            
            foreach (var evt in Events.GetAfter(goal))
            {
                if (evt.IsAsync)
                    _ = evt.Handler(goal);
                else
                    await evt.Handler(goal);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return GoalResult.Error(ex.Message, ex);
        }
        finally
        {
            CallStack.Pop();
        }
    }
}
