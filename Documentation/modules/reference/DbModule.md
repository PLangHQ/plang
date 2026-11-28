# DbModule

Hint: `[db]`  
Type: `PLang.Modules.DbModule.Program`

Database access, select, insert, update, delete and execute raw sql, on a table of any name. A step naming a datasource, e.g. `ds: "dev"`, is database access, except `load %x%` and `store %x%` of a %variable%, which belong to the variable module. Handles transactions. Sets and create datasources. Isolated data pattern (idp)

## Methods

### BeginTransaction

```
BeginTransaction(List<String> dataSourceNames = null, PLang.Models.GoalToCallInfo onRollback = null) : object
```

- `dataSourceNames` *List<String>*, default `null`
- `onRollback` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)

### CreateDataSource

```
CreateDataSource(String name = data, String databaseType = sqlite, Nullable<Boolean> setAsDefaultForApp = null, Nullable<Boolean> keepHistoryEventSourcing = null) : PLang.Modules.DbModule.ModuleSettings+DataSource
```

Create a datasource to a database


### CreateTable

```
CreateTable(String sql) : Int64
```

When user does not define a primary key, add it to the create statement as id column not null, when KeepHistory is set to false, make the column auto increment. When renaming a table and dropping, include  PRAGMA foreign_keys=OFF; at start and PRAGMA foreign_keys=ON; PRAGMA foreign_key_check; at the end


### Delete

```
Delete(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Int64
```

- `dataSourceName` *String*
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)
- `validateAffectedRows` *Boolean*, default `True`

### EndTransaction

```
EndTransaction() : object
```


### Execute

```
Execute(String sql, List<String> tableAllowList = null, String dataSourceName = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64
```

Executes a sql statement that defined by user, such as `create index ..`, `drop ..`. Preferable not for select,update,insert statements. This statement will be validated. Since this is pure and dynamic execution on database, user MUST to define list of tables that are allowed to be updated

- `sql` *String*
- `tableAllowList` *List<String>*, default `null`
- `dataSourceName` *String*, default `null`
- `parameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

### ExecuteByConnectionString

```
ExecuteByConnectionString(String sql, String connectionString, String dbType = sqlite, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64
```

Execute sql statement on specific connection string

- `sql` *String*
- `connectionString` *String*
- `dbType` *String*, default `sqlite`
- `parameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

### ExecuteDynamicSql

```
ExecuteDynamicSql(String dataSourceName, String sql, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64
```

Executes a sql statement that is fully dynamic or from a %variable%. Since this is pure and dynamic execution on database, user MUST to define list of tables that are allowed to be updated

- `dataSourceName` *String*
- `sql` *String*
- `tableAllowList` *List<String>*, default `null`
- `parameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

### ExecutePlang

```
ExecutePlang(String sql, List<PLang.Runtime.ObjectValue> parameters = null) : Int64
```

Reference as db.Execute(%sql%, %parameters%)

- `sql` *String*
- `parameters` *List<PLang.Runtime.ObjectValue>*, default `null` — (see Type information in SupportingObjects)

### ExecuteSqlByConnectionString

```
ExecuteSqlByConnectionString(String pathToSql, String sql, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64
```

Execute sql by connection string

- `pathToSql` *String*
- `sql` *String*
- `tableAllowList` *List<String>*, default `null`
- `parameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

### ExecuteSqlFile

```
ExecuteSqlFile(String dataSourceName, String fileName, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : Int64
```

Executes a sql file that writes (update, insert, delete), e.g. `execute sql/mark.sql, email=%email%`. The sql is written in the file with @name for each value, and the step passes the values as name=value

- `dataSourceName` *String*
- `fileName` *String*
- `tableAllowList` *List<String>*, default `null`
- `parameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — One entry per named value the sql file expects. The step writes each as name=value, with or without the word parameters in front, e.g. `email=%email%` is one entry. ParameterName is that name with an @ in front, VariableNameOrValue is the value. This is always a list, never the bare value (see Type information in SupportingObjects)

### GetAdditionalAssistantErrorInfo

```
GetAdditionalAssistantErrorInfo() : String
```


### GetAdditionalSystemErrorInfo

```
GetAdditionalSystemErrorInfo() : String
```


### GetDatabaseStructure

```
GetDatabaseStructure(String dataSourceName, List<String> tables = null) : IEnumerable<TableInfo>
```

Returns tables and views in database with the columns description


### GetDataSource

```
GetDataSource(String name = null) : PLang.Modules.DbModule.ModuleSettings+DataSource
```

gets the current datasource by name


### GetDataSources

```
GetDataSources() : PLang.Modules.DbModule.ModuleSettings+DataSource
```

gets all databases that have been created


### GetDbScheme

```
GetDbScheme(String dataSourceName) : List<String>
```

Return list of tables and views in a datasource


### Insert

```
Insert(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Int64
```

Insert into table. Will return affected row count. Choose when user doesn't write result into %variable%

- `dataSourceName` *String*
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)
- `validateAffectedRows` *Boolean*, default `True`

### InsertAndSelectIdOfInsertedRow

```
InsertAndSelectIdOfInsertedRow(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Object
```

Insert statement that will return the id of the inserted row.  Used when user intends to write into a %id%

- `dataSourceName` *String*
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)
- `validateAffectedRows` *Boolean*, default `True`

### InsertBulk

```
InsertBulk(String dataSourceName, String tableName, List<Object> itemsToInsert = null, Dictionary<String, Object> columnMapping = null, Boolean ignoreContraintOnInsert = False) : Int64
```

ONLY When inserting list of items(%variables% is plural). Insert a list(bulk) into database, return number of rows inserted. columnMapping maps which variable should match with a column. User will define that he is using bulk insert.


### InsertOrUpdate

```
InsertOrUpdate(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Int64
```

Insert or update table(Upsert). Will return affected row count. Choose when user doesn't write result into %variable%

- `dataSourceName` *String*
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)
- `validateAffectedRows` *Boolean*, default `True`

### InsertOrUpdateAndSelectIdOfRow

```
InsertOrUpdateAndSelectIdOfRow(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : Object
```

Insert or update table(Upsert). Will return the id/primary key of the affected row. Used when user intends to write into a %id%

- `dataSourceName` *String*
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

### LoadExtension

```
LoadExtension(String dataSourceName, String fileName, String procName = null) : object
```


### QueryDynamicSql

```
QueryDynamicSql(String sql, List<String> tableAllowList = null, List<String> dataSourceNames = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null) : PLang.Modules.DbModule.Table
```

Query sql statement that is fully from a %variable%. Since this is pure and dynamic execution on database, user MUST to define list of tables that are allowed to be queried

- `sql` *String*
- `tableAllowList` *List<String>*, default `null`
- `dataSourceNames` *List<String>*, default `null`
- `parameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

Examples:

- execute query %sql%, ds: %dataSource%, tables: users, products, write to %result% => sql=%sql%, dataSouceName=%dataSource%, tableAllowList=["users", "products"]

### QuerySqlFile

```
QuerySqlFile(List<String> dataSourceNames = null, String fileName, List<String> tableAllowList = null, List<PLang.Modules.DbModule.Program+ParameterInfo> parameters = null, Nullable<Int32> rowsToReturn = null) : Object
```

Query the database with a sql file pointed to by path, e.g. query sql/file.sql. It can use multiple datasource and parameterer. This is where a long query belongs (many lines, several CTEs, window functions): the sql is written in the file with @name for each value, and the step passes the values, `query sql/trends.sql, name=%name%, tab=%tab%, write to %rows%`. The file is run as it is, so a % inside it, e.g. like 'x%', is never a variable. When the step also says to return 1 row, set rowsToReturn=1: the file is still the sql, never write sql of your own

- `dataSourceNames` *List<String>*, default `null`
- `fileName` *String*
- `tableAllowList` *List<String>*, default `null`
- `parameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — One entry per named value the sql file expects. The step writes each as name=value, with or without the word parameters in front, e.g. `kt=%kt%` is one entry. ParameterName is that name with an @ in front, VariableNameOrValue is the value. This is always a list, never the bare value: parameters="%kt%" binds nothing (see Type information in SupportingObjects)
- `rowsToReturn` *Nullable<Int32>*, default `null`

Examples:

- query usersCount.sql, parameters: date=%now%, ds:"data", "sales", table: "users", write to %result% => fileName="usersCount.sql", parameters=[{"TypeFullName":"System.Object","ParameterName":"@date","VariableNameOrValue":"%now%"}], dataSourceNames=["data", "sales"], tableAllowList=["users"], ReturnValues="%result%"
- query usersCount.sql, table: "users", write to %result% => fileName="usersCount.sql", tableAllowList=["users"], ReturnValues="%result%"
- query sql/totalProducts.sql, table: "products", write to %productCount% => fileName="totalProducts.sql", tableAllowList=["products"], ReturnValues="%productCount%"
- query sql/lookup.sql, kt=%kt%, table: "people", write to %person% => fileName="lookup.sql", parameters=[{"TypeFullName":"System.Object","ParameterName":"@kt","VariableNameOrValue":"%kt%"}], tableAllowList=["people"], ReturnValues="%person%"

### ResetSetup

```
ResetSetup(List<String> dataSourceNames = null) : object
```


### Rollback

```
Rollback() : object
```


### Select

```
Select(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : PLang.Modules.DbModule.Table
```

Any step starting with SELECT/WITH using one datasource. Every %variable% in the step, wherever it stands in the sql (where, case, join, a CTE, a subquery, a window function), is replaced by an @parameter in the sql text and listed once in sqlParameters, the same @parameter at every place the %variable% is used. The sql you return contains no %variable%, the length of the sql makes no difference. A step that carries a long query (many lines, several CTEs) is better written as a sql file, `query sql/file.sql, name=%name%`

- `dataSourceName` *String*
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

Examples:

- select * from orders where id=%id%, ds: users/%user.id%, write to %orders% => sql="select * from orders where id=@id", sqlParameters=[{"TypeFullName":"System.Int64","ParameterName":"@id","VariableNameOrValue":"%id%"}], dataSourceName="users/%user.id%", ReturnValues=["%orders%"]
- with per as (select case %tab% when 'week' then 7 else 1 end as d), recent as (select * from events where server = %name% and day > date('now', '-' || (select d from per) || ' day')) select kind, count(*) from recent where server = %name% group by kind, write to %rows% => sql="with per as (select case @tab when 'week' then 7 else 1 end as d), recent as (select * from events where server = @name and day > date('now', '-' || (select d from per) || ' day')) select kind, count(*) from recent where server = @name group by kind", sqlParameters=[{"TypeFullName":"System.String","ParameterName":"@tab","VariableNameOrValue":"%tab%"},{"TypeFullName":"System.String","ParameterName":"@name","VariableNameOrValue":"%name%"}], ReturnValues=["%rows%"]

### SelectOneRow

```
SelectOneRow(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : Object
```

When SELECT/WITH should return 1 row (limit 1). Every %variable% in the step, wherever it stands in the sql, is an @parameter in the sql text and listed once in sqlParameters, the sql text holds no %variable%

- `dataSourceName` *String*
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

### SelectOneRowWithMultipleDataSources

```
SelectOneRowWithMultipleDataSources(List<String> dataSourceNames = null, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : Object
```

When SELECT/WITH should return 1 row (limit 1) on multiple data source

- `dataSourceNames` *List<String>*, default `null`
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

### SelectWithMultipleDataSources

```
SelectWithMultipleDataSources(List<String> dataSourceNames = null, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null) : PLang.Modules.DbModule.Table
```

Doing SELECT/WITH on multiple datasources

- `dataSourceNames` *List<String>*, default `null`
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

Examples:

- select * from main.users u, join users.orders o on o.userId=u.id where u.id=%id%, ds: data, users/%user.id%, write to %results% => sql="select * from main.users u join users.orders o on o.userId=u.id where u.id=@id", sqlParameters=[{"TypeFullName":"System.Int64","ParameterName":"@id","VariableNameOrValue":"%id%"}], dataSourceName=["data", "users/%user.id%"], ReturnValues=["%results%"]

### SetDataSourceNames

```
SetDataSourceNames(List<String> dataSourceNames = null) : PLang.Modules.DbModule.ModuleSettings+DataSource
```

set the current datasource by name(s). This could be one datasource or many


### Update

```
Update(String dataSourceName, String sql, List<PLang.Modules.DbModule.Program+ParameterInfo> sqlParameters = null, Boolean validateAffectedRows = True) : Int64
```

- `dataSourceName` *String*
- `sql` *String*
- `sqlParameters` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)
- `validateAffectedRows` *Boolean*, default `True`

### UpdateWithJsonColumns

```
UpdateWithJsonColumns(String dataSourceName, String table, String jsonOfColumns, List<String> allowColumns = null, String whereStatment = null, List<PLang.Modules.DbModule.Program+ParameterInfo> whereParameter = null) : Int64
```

Allows user to send in json of columns to update. The json can be a %variable%. allowColums is the columns that are allowed to be updated. example: ` update table users with %json% where %id%, allowed columns: name, phone`

- `dataSourceName` *String*
- `table` *String*
- `jsonOfColumns` *String*
- `allowColumns` *List<String>*, default `null`
- `whereStatment` *String*, default `null`
- `whereParameter` *List<PLang.Modules.DbModule.Program+ParameterInfo>*, default `null` — (see Type information in SupportingObjects)

