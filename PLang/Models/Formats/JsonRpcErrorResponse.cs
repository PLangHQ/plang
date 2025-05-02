using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLang.Models.Formats
{
	public class JsonRpcError
	{
		[JsonPropertyName("code")]
		public int Code { get; set; }
		[JsonPropertyName("message")]
		public string Message { get; set; }
		[JsonPropertyName("data")]
		public object Data { get; set; } // optional
	}

	public class JsonRpcErrorResponse
	{
		[JsonPropertyName("jsonrpc")]
		public string Jsonrpc { get; set; } = "2.0";
		[JsonPropertyName("error")]
		public JsonRpcError Error { get; set; }
		[JsonPropertyName("id")]
		public object Id { get; set; }
		
	}

	public class Payload
	{
		[JsonPropertyName("response")]
		public JsonRpcErrorResponse Response { get; set; }
		[JsonPropertyName("signature")]
		public object Signature { get; set; }
	}
}
