using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using PLang.Utils;

namespace PLang.Models
{
	public class Setting
	{
		public Setting() { }
		public Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string Value, DateTime? Created = null, string SignatureData = null)
		{
			this.AppId = AppId;
			this.ClassOwnerFullName = ClassOwnerFullName;
			this.ValueType = ValueType;
			this.Key = Key;
			this.Value = Value;
			this.signature = (string.IsNullOrEmpty(SignatureData)) ? [] : JsonConvert.DeserializeObject<Dictionary<string, object>>(SignatureData) ?? [];
			this.Created = Created ?? SystemTime.Now();
		}

		public Setting(string AppId, string ClassOwnerFullName, string ValueType, string Key, string Value, Dictionary<string, object> SignatureData, DateTime? Created = null)
		{
			this.AppId = AppId;
			this.ClassOwnerFullName = ClassOwnerFullName;
			this.ValueType = ValueType;
			this.Key = Key;
			this.Value = Value;
			this.signature = SignatureData;
			this.Created = Created ?? SystemTime.Now();
		}


		public string AppId { get; set; }
		public string ClassOwnerFullName { get; set; }
		public string ValueType { get; set; }
		public string Key { get; set; }
		public string Value { get; set; }
		public DateTime? Created { get; set; }
		string signatureData;
		public string SignatureData { 
			get { return signatureData; }
			set { signature = (string.IsNullOrEmpty(value)) ? [] : JsonConvert.DeserializeObject<Dictionary<string, object>>(value) ?? []; } 
		}

		private Dictionary<string, object> signature = new();
		public Dictionary<string, object> Signature { get { return signature; } }

	}
}
