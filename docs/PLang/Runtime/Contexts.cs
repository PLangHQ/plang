namespace PLang.Runtime;

/// <summary>
/// Application-level context. Lives for the lifetime of the application.
/// Contains system-level settings and state.
/// </summary>
public partial class PLangAppContext
{
    public DateTime StartTime { get; }
    public string? ApplicationPath { get; set; }
    public Properties Properties { get; }
    
    public PLangAppContext()
    {
        StartTime = DateTime.UtcNow;
        Properties = new Properties();
    }
}

/// <summary>
/// Request/execution-level context. Created per request, job, or execution unit.
/// Contains user-level settings and state.
/// </summary>
public partial class PLangContext
{
    public string? ExecutionId { get; set; }
    public DateTime StartTime { get; }
    public Properties Properties { get; }
    
    public PLangContext()
    {
        ExecutionId = Guid.NewGuid().ToString();
        StartTime = DateTime.UtcNow;
        Properties = new Properties();
    }
}
