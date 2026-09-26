namespace app.module.action.http;

/// <summary>
/// Compile-time URL-extension → PLang type inference shared by http.request
/// and http.upload. A literal Url with a recognized extension surfaces a type
/// so the trailing variable.set can stamp Response.Body's expected shape.
/// Variable references and unknown extensions return bare Ok().
/// </summary>
internal static class HttpBuildHelpers
{
    public static Task<data.@this> InferTypeFromUrl(
        global::app.goal.step.action.@this? action,
        global::app.@this? app,
        string paramName)
    {
        // A url marked a template holds a variable with no binding at build — the row's marker says
        // so, never the characters in it.
        var row = action?[paramName];
        var raw = row?.Value?.ToString();
        if (string.IsNullOrEmpty(raw) || row!.Type?.Template != null)
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
        if (app == null) return Task.FromResult(data.@this.Ok());

        // The same {name, kind} derivation file.read uses, so the URL-extension build
        // stamp matches the runtime response-body stamp.
        var inferred = app.Type.Extension(ext);
        if (inferred.IsNull || !app.Type.Contains(inferred.Name))
            return Task.FromResult(data.@this.Ok());

        return Task.FromResult(app.User.Context.Ok(inferred));
    }
}
