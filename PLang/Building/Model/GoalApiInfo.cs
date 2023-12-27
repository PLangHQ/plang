namespace PLang.Building.Model
{
	public record GoalApiInfo(string Method, string Description, string ContentEncoding = "utf-8", string ContentType = "application/json", 
		int MaxContentLengthInBytes = 4194304, string? CacheControlPrivateOrPublic = null, int? CacheControlMaxAge = null, string? NoCacheOrNoStore = null);

}
