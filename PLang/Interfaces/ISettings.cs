using PLang.Models;

namespace PLang.Interfaces
{
    public interface ISettings
	{
		public static readonly string GoalFileName = "00. Goal.pr";

		string AppId { get; }

		bool Contains<T>(Type callingType, string? key = null);
		T Get<T>(Type callingType, string key, T defaultValue, string explain);
		T? GetOrDefault<T>(Type callingType, string key, T defaultValue);
		void Set<T>(Type callingType, string key, T value);
		void Remove<T>(Type callingType, string? key = null);
		IEnumerable<Setting> GetAllSettings();
		List<T> GetValues<T>(Type callingType, string? key = null);
		void SetList<T>(Type callingType, T value, string? key = null);

		string GetSalt();
		void SetSharedSettings(string appId);
		string SerializeSettings();
	}
}
