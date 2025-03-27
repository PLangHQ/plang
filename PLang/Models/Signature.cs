using System.Runtime.Serialization;

namespace PLang.Models
{
	public class Signature : BaseModel
	{
		public Identity? Identity { get; set; }
		public DateTimeOffset? ExpiresInMs { get; set; }
		public string Nonce { get; set; }
		public string SignedData { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public bool IsVerified { get; set; } = false;
		public DateTimeOffset Created { get; set; }
		public List<string> Contracts { get; set; }
		public Signature? Parent { get; set; }

		

	}
}
