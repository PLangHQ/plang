using PLang.Models;

namespace PLang.Modules.WebserverModule
{
	public record CacheInfo(long CacheTimeoutInMilliseconds, string CacheKey, int CacheType = 1);

	public interface IRouting
	{
		string Path { get; }
		string? ContentType { get; }
		CacheInfo? CacheInfo { get; }
	}


	public record FolderRouting(string Path, string? Folder = null, string? Method = null, string? ContentType = null,
								Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null,
								string? DefaultResponseContentEncoding = null, CacheInfo? CacheInfo = null) : IRouting;

	public record GoalRouting(string Path, GoalToCall? GoalToCall = null, string? Method = null, string? ContentType = null,
								Dictionary<string, object?>? Parameters = null, long? MaxContentLength = null,
								string? DefaultResponseContentEncoding = null, CacheInfo? CacheInfo = null) : IRouting;

	public record StaticFileRouting(string Path, string FileName, string? ContentType = null, CacheInfo? CacheInfo = null) : IRouting;


}
