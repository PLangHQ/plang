namespace PLang.Runtime.Modules;

/// <summary>
/// Example Database module demonstrating the object-based pattern:
/// - Typed request objects (DbQuery, DbInsert, DbUpdate, DbDelete)
/// - Convenience methods that call Execute
/// - Injectable executor
/// </summary>
public class DbModule : BaseModule
{
    public override string Name => "db";
    
    // Injectable executor
    private Func<object, Task<GoalResult>>? _customExecutor;
    
    public void SetExecutor(string pathOrDll)
    {
        if (pathOrDll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            // Load from DLL
            throw new NotImplementedException("DLL executor loading not implemented");
        }
        else
        {
            _customExecutor = async (request) => await Engine.Run(pathOrDll, request);
        }
    }
    
    // Typed convenience methods
    public Task<GoalResult> Select(DbQuery query) => Execute("select", query);
    public Task<GoalResult> Insert(DbInsert insert) => Execute("insert", insert);
    public Task<GoalResult> Update(DbUpdate update) => Execute("update", update);
    public Task<GoalResult> Delete(DbDelete delete) => Execute("delete", delete);
    
    public override Task<GoalResult> Execute(string method, object? data)
    {
        if (data == null)
            return GoalResult.ErrorTask("Request data is required");
        
        if (_customExecutor != null)
            return _customExecutor(data);
        
        return method switch
        {
            "select" => ExecuteSelect((DbQuery)data),
            "insert" => ExecuteInsert((DbInsert)data),
            "update" => ExecuteUpdate((DbUpdate)data),
            "delete" => ExecuteDelete((DbDelete)data),
            _ => GoalResult.ErrorTask($"Unknown method: {method}")
        };
    }
    
    protected virtual Task<GoalResult> ExecuteSelect(DbQuery query)
    {
        // Actual implementation would use ADO.NET or Dapper
        throw new NotImplementedException();
    }
    
    protected virtual Task<GoalResult> ExecuteInsert(DbInsert insert)
    {
        throw new NotImplementedException();
    }
    
    protected virtual Task<GoalResult> ExecuteUpdate(DbUpdate update)
    {
        throw new NotImplementedException();
    }
    
    protected virtual Task<GoalResult> ExecuteDelete(DbDelete delete)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Query request - SELECT operations
/// </summary>
public record DbQuery(string Sql, object? Parameters = null)
{
    public string? DataSource { get; set; }
    public bool ReturnOne { get; set; }
    public string? WriteTo { get; set; }
}

/// <summary>
/// Insert request - one cohesive object with table + data
/// </summary>
public record DbInsert(string Table, object Data)
{
    public string? DataSource { get; set; }
    public string? WriteTo { get; set; }
}

/// <summary>
/// Update request
/// </summary>
public record DbUpdate(string Table, object Data, string Where, object? WhereParameters = null)
{
    public string? DataSource { get; set; }
    public string? WriteTo { get; set; }
}

/// <summary>
/// Delete request
/// </summary>
public record DbDelete(string Table, string Where, object? WhereParameters = null)
{
    public string? DataSource { get; set; }
}
