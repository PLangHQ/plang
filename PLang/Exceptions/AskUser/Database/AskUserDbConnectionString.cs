namespace PLang.Exceptions.AskUser.Database
{
	public class AskUserDbConnectionString : AskUserException
	{
		private readonly string typeFullName;
		private readonly bool setAsDefaultForApp;
		private readonly bool keepHistoryEventSourcing;
		private readonly string dataSourceName;
		string regexToExtractDatabaseNameFromConnectionString;
		private readonly bool keepHistory;
		private readonly bool isDefault;

		public AskUserDbConnectionString(string dataSourceName, string typeFullName,
			string regexToExtractDatabaseNameFromConnectionString, bool keepHistory, bool isDefault, string question,
				Func<string, string, string, string, bool, bool, Task> callback) : base(question, CreateAdapter(callback))
		{
			this.dataSourceName = dataSourceName;
			this.regexToExtractDatabaseNameFromConnectionString = regexToExtractDatabaseNameFromConnectionString;
			this.keepHistory = keepHistory;
			this.isDefault = isDefault;
			this.typeFullName = typeFullName;

		}

		public override async Task InvokeCallback(object answer)
		{
			if (Callback == null) return;

			await Callback.Invoke([dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, answer, keepHistory, isDefault]);

		}
	}




}
