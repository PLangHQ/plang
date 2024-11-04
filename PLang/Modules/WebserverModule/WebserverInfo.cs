

using System.Net;

namespace PLang.Modules.WebserverModule
{
	public record WebserverInfo(HttpListener Listener, string WebserverName, string Scheme, string Host, int Port,
				long DefaultMaxContentLengthInBytes = 4096 * 1024, string DefaultResponseContentEncoding = "utf-8", bool SignedRequestRequired = false)
	{
		public List<IRouting> Routings { get; set; } = new();
	}
}
