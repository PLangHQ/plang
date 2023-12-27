using IdGen;
using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Interfaces
{
    public class Setting
    {
		public Setting() { }
        public Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string? Value, DateTime? Created = null)
        {
			this.AppId = AppId;
			this.ClassOwnerFullName = ClassOwnerFullName;
			this.ValueType = ValueType;
			this.Key = Key;
			this.Value = Value;
			this.Created = Created ?? DateTimeOffset.UtcNow.DateTime;
		}

		public string AppId { get; }
		public string ClassOwnerFullName { get; }
		public string ValueType { get; }
		public string Key { get; }
		public string? Value { get; }
		public DateTime? Created { get; }
		public bool IsGlobal { get; set; }
	}
	/*
    public record Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string? Value, DateTime? Created)
    {
		public Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string? Value) : this(AppId, ClassOwnerFullName, ValueType, Key, Value, DateTimeOffset.UtcNow.DateTime)
		{
		}
        public bool IsGlobal { get; set; }
	};*/

    public interface ISettingsRepository
    {
        public void Init();
        public IEnumerable<Setting> GetSettings();
        public void Set(Setting setting);
        void Remove(Setting setting);
		LlmQuestion? GetLlmCache(string hash);
		void SetLlmCache(string hash, LlmQuestion llmQuestion);
	}
}
