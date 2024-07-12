using Microsoft.Data.Sqlite;
using PLang.Errors.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions.AskUser.Database
{
    public class AskUserSqliteName : AskUserError
	{
		private readonly string rootPath;

		public AskUserSqliteName(string rootPath, string question, Func<string, string, string, string, bool, bool, Task> callback) : base(question, CreateAdapter(callback))
		{
			this.rootPath = rootPath;
		}

		public override async Task InvokeCallback(object answer)
		{
			if (Callback == null) return;

			var dbName = answer.ToString()!.Replace(" ", "_").Replace(".sqlite", "");
			string dbPath = "." + Path.DirectorySeparatorChar + ".db" + Path.DirectorySeparatorChar + dbName + ".sqlite";
			string dbAbsolutePath = Path.Join(rootPath, dbPath);

			await Callback.Invoke([
				dbName.ToString(),
				typeof(SqliteConnection).FullName!,
				dbName.ToString() + ".sqlite",
				$"Data Source={dbAbsolutePath};Version=3;",
				true,
				false]);

		}
	}
}
