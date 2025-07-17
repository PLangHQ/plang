using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Utils;
using System.Runtime.Serialization;

namespace PLang.Models
{
	public record HashedData(string Type, string Format, string Hash);

	public record SignedMessage
	{
		[JsonProperty("type", Order = 1)]
		public string Type { get; set; } = "Ed25519";

		[JsonProperty("nonce", Order = 2)]
		public string Nonce { get; set; } = Guid.NewGuid().ToString();

		[JsonProperty("created", Order = 3)]
		public DateTimeOffset Created { get; set; } = SystemTime.OffsetUtcNow();
		
		[JsonProperty("expires", Order = 4)]
		public DateTimeOffset? Expires { get; set; }		

		[JsonProperty("name", Order = 6)]
		public string? Name { get; set; }

		[JsonProperty("data", Order = 7)]
		public HashedData? Data { get; set; }

		[JsonProperty("contracts", Order = 8)]
		public List<string> Contracts { get; set; }

		[JsonProperty("headers", Order = 9)]
		public Dictionary<string, object?>? Headers { get; set; }

		[JsonProperty("parent", Order = 10)]
		public SignedMessage? Parent { get; set; }

		[JsonProperty("identity", Order = 11)]
		public string? Identity { get; set; }

		[JsonProperty("signature", Order = 12)]
		public string? Signature { get; set; }



		public static List<string> DefaultContracts = ["C0"];
	}

	public record SignedMessageJwkIdentity : SignedMessage
	{
		[JsonProperty("jwkIdentity", Order = 13)]
		public JToken? JwkIdentity {  get; set; }
	}
}
