using PLang.Errors;
using PLang.Errors.Handlers;

namespace PLang.Exceptions.AskUser.Database
{
    public class AskUserDbConnectionString : AskUserError
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

		public override async Task<IError?> InvokeCallback(object answer)
		{
			if (Callback == null) return null;

			return await Callback.Invoke([dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, answer, keepHistory, isDefault]);

		}
	}




}
