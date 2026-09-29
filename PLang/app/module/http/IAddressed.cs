namespace app.module.http;

/// <summary>An http action that goes to a url — request, download, upload: its written <c>Url</c>, and the
/// <c>BaseUrl</c> a relative one joins.</summary>
public interface IAddressed
{
    data.@this<global::app.type.item.text.@this> Url { get; }
    data.@this<global::app.type.item.path.http.@this>? BaseUrl { get; }
    actor.context.@this Context { get; }

    /// <summary>The url the action goes to — what the build expects and what the run requests, one answer: a
    /// relative Url joined onto BaseUrl, any other an http url of its own (a bare host is https); a relative
    /// one with no base is NoBaseUrl, another scheme InvalidUrlScheme.</summary>
    async System.Threading.Tasks.Task<data.@this<global::app.type.item.path.http.@this>> Target()
    {
        var addressed = await Url.Use(async url =>
        {
            // a base that holds nothing (the slot unset) is no base
            if (BaseUrl is { IsInitialized: true })
                return await BaseUrl.Use(@base => System.Threading.Tasks.Task.FromResult<data.@this>(@base.Address(url, Context)));
            var carrier = new data.@this<global::app.type.item.path.http.@this>("url", context: Context);
            return global::app.type.item.path.http.@this.Create(url, carrier) is { } made ? Context.Ok<global::app.type.item.path.http.@this>(made) : carrier;
        });
        return data.@this<global::app.type.item.path.http.@this>.From(addressed);
    }
}
