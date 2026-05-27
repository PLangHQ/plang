namespace app.modules.http;

/// <summary>
/// Compile-time URL-extension → PLang type inference shared by http.request
/// and http.upload. A literal Url with a recognized extension surfaces a type
/// so the trailing variable.set can stamp Response.Body's expected shape.
/// Variable references and unknown extensions return bare Ok().
/// </summary>
internal static class HttpBuildHelpers
{
    public static Task<data.@this> InferTypeFromUrl(
        global::app.goals.goal.steps.step.actions.action.@this? action,
        global::app.@this? app,
        string paramName)
    {
        var raw = action?.Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, paramName, System.StringComparison.OrdinalIgnoreCase))?.Value as string;
        if (string.IsNullOrEmpty(raw) || raw.Contains('%'))
            return Task.FromResult(data.@this.Ok());

        // Trim query / fragment so they don't leak into the extension scan.
        var clean = raw;
        var q = clean.IndexOfAny(new[] { '?', '#' });
        if (q >= 0) clean = clean[..q];

        var lastDot = clean.LastIndexOf('.');
        var lastSep = clean.LastIndexOfAny(new[] { '/', '\\' });
        if (lastDot <= lastSep || lastDot < 0 || lastDot == clean.Length - 1)
            return Task.FromResult(data.@this.Ok());

        var ext = clean[lastDot..];
        var mime = app?.Formats.Mime(ext) ?? "application/octet-stream";
        if (mime == "application/octet-stream") return Task.FromResult(data.@this.Ok());

        return Task.FromResult(data.@this.Ok(ext.TrimStart('.').ToLowerInvariant()));
    }
}
