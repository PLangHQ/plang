using IdGen;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Interfaces
{
    public class Setting
    {
		public Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string Value, string SignatureData, DateTime? Created = null)
		{
			this.AppId = AppId;
			this.ClassOwnerFullName = ClassOwnerFullName;
			this.ValueType = ValueType;
			this.Key = Key;
			this.Value = Value;
			this.SignatureData = (string.IsNullOrEmpty(SignatureData)) ? new() : JsonConvert.DeserializeObject<Dictionary<string, object>>(SignatureData) ?? new();
			this.Created = Created ?? DateTimeOffset.UtcNow.DateTime;
		}
			public Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string Value, Dictionary<string, object> SignatureData, DateTime? Created = null)
        {
			this.AppId = AppId;
			this.ClassOwnerFullName = ClassOwnerFullName;
			this.ValueType = ValueType;
			this.Key = Key;
			this.Value = Value;
			this.SignatureData = SignatureData;
			this.Created = Created ?? DateTimeOffset.UtcNow.DateTime;
		}


		public string AppId { get; }
		public string ClassOwnerFullName { get; }
		public string ValueType { get; }
		public string Key { get; }
		public string Value { get; }
		public DateTime? Created { get; }
		public Dictionary<string, object> SignatureData { get; }

	}

    public interface ISettingsRepository
    {
        public void Init();
        public IEnumerable<Setting> GetSettings();
        public void Set(Setting setting);
        void Remove(Setting setting);
		LlmRequest? GetLlmRequestCache(string hash);
		void SetLlmRequestCache(string hash, LlmRequest question);
	}
}
