using Newtonsoft.Json;
using PLang.Utils;

namespace PLang.Models
{
	public class Setting
	{
		public Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string Value, DateTime? Created = null, string? SignatureData = null)
		{
			this.AppId = AppId;
			this.ClassOwnerFullName = ClassOwnerFullName;
			this.ValueType = ValueType;
			this.Key = Key;
			this.Value = Value;
			this.SignatureData = (string.IsNullOrEmpty(SignatureData)) ? [] : JsonConvert.DeserializeObject<Dictionary<string, object>>(SignatureData) ?? [];
			this.Created = Created ?? SystemTime.Now();
		}

		public Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string Value, Dictionary<string, object> SignatureData, DateTime? Created = null)
		{
			this.AppId = AppId;
			this.ClassOwnerFullName = ClassOwnerFullName;
			this.ValueType = ValueType;
			this.Key = Key;
			this.Value = Value;
			this.SignatureData = SignatureData;
			this.Created = Created ?? SystemTime.Now();
		}


		public string AppId { get; }
		public string ClassOwnerFullName { get; }
		public string ValueType { get; }
		public string Key { get; }
		public string Value { get; }
		public DateTime? Created { get; }
		public Dictionary<string, object> SignatureData { get; }

	}
}
