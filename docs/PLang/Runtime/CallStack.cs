using System.Text;

namespace PLang.Runtime;

public partial class CallStack
{
    private readonly Stack<CallFrame> _frames = new();
    
    public IReadOnlyList<CallFrame> Frames => _frames.Reverse().ToList();
    public CallFrame? Current => _frames.Count > 0 ? _frames.Peek() : null;
    public int Depth => _frames.Count;
    
    public void Push(Goal goal)
    {
        _frames.Push(new CallFrame(goal, null));
    }
    
    public void Push(Step step)
    {
        var currentGoal = Current?.Goal;
        _frames.Push(new CallFrame(currentGoal, step));
    }
    
    public void Pop()
    {
        if (_frames.Count > 0)
            _frames.Pop();
    }
    
    public void Clear()
    {
        _frames.Clear();
    }
    
    public string GetStackTrace()
    {
        var sb = new StringBuilder();
        foreach (var frame in _frames.Reverse())
        {
            sb.AppendLine(frame.ToString());
        }
        return sb.ToString();
    }
}
