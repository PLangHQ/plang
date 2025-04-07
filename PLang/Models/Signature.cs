using PLang.Utils;
using System.Runtime.Serialization;

namespace PLang.Models
{
	public class Signature : BaseModel
	{
		public static List<string> DefaultContracts = ["C0"];
		public string? Identity { get; set; }
		public string? Name { get; set; }
		public DateTimeOffset? ExpiresInMs { get; set; }
		public string Nonce { get; set; } = Guid.NewGuid().ToString();
		public string? SignedData { get; set; }
		public string? Body { get; set; }
		public DateTimeOffset Created { get; set; } = SystemTime.OffsetUtcNow();
		public List<string> Contracts { get; set; }
		public string Type { get; set; } = "Ed25519";
		public Dictionary<string, object?>? Headers { get; set; }

		public Signature? Parent { get; set; }
	}
}
