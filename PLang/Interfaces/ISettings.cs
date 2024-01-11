

using PLang.Building.Model;

namespace PLang.Interfaces
{
	public interface ISettings
	{
		public static readonly string GoalFileName = "00. Goal.pr";

		string AppId { get; }
		string BuildPath { get; }
		string GoalsPath { get; }
		bool Contains<T>(Type callingType, string? key = null);
		T? Get<T>(Type callingType, string key, T defaultValue, string explain);
		T? GetOrDefault<T>(Type callingType, string key, T defaultValue);
		void Set<T>(Type callingType, string key, T value);
		void Remove<T>(Type callingType, string? key = null);
		List<Setting> GetAllSettings();
		List<T> GetValues<T>(Type callingType);
		void SetList<T>(Type callingType, T value, string? key = null);
		LlmQuestion? GetLlmQuestion(string hash);
		void SetLlmQuestion(string hash, LlmQuestion question);
		void SetLlmQuestion(string hash, LlmRequest question);
		LlmRequest? GetLlmRequest(string hash);
	}
}
