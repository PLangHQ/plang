namespace app.module.list.type.query;

/// <summary>The order a query's parts run in: SQL's — where, group, distinct, order — whatever order the step
/// says them in, or the order the step writes them.</summary>
[global::app.Attributes.PlangType("execution")]
public enum execution
{
    sql,
    written,
}
