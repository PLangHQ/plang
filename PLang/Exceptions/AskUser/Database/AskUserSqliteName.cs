using Microsoft.Data.Sqlite;
using PLang.Errors.Handlers;

namespace PLang.Exceptions.AskUser.Database;

public class AskUserSqliteName : AskUserError
{
    private readonly string rootPath;

    public AskUserSqliteName(string rootPath, string question,
        Func<string, string, string, string, bool, bool, Task> callback) : base(question, CreateAdapter(callback))
    {
        this.rootPath = rootPath;
    }

    public override async Task InvokeCallback(object answer)
    {
        if (Callback == null) return;

        var dbName = answer.ToString()!.Replace(" ", "_").Replace(".sqlite", "");
        var dbPath = "." + Path.DirectorySeparatorChar + ".db" + Path.DirectorySeparatorChar + dbName + ".sqlite";
        var dbAbsolutePath = Path.Join(rootPath, dbPath);

        await Callback.Invoke([
            dbName,
            typeof(SqliteConnection).FullName!,
            dbName + ".sqlite",
            $"Data Source={dbAbsolutePath};Version=3;",
            true,
            false
        ]);
    }
}