using System.Net.Http.Headers;

namespace PLang.Runtime.Modules;

/// <summary>
/// Example HTTP module demonstrating the object-based pattern:
/// - Typed request object (HttpRequest)
/// - Convenience methods (Get, Post, etc.) that call Execute
/// - Injectable executor (DLL or goal path)
/// - Stream-based serialization
/// </summary>
public class HttpModule : BaseModule
{
    public override string Name => "http";
    
    // Injectable request executor - can be DLL or goal
    private Func<HttpRequest, Task<GoalResult>>? _customExecutor;
    
    /// <summary>
    /// Set a custom executor - can be a DLL path or a goal path
    /// </summary>
    public void SetExecutor(string pathOrDll)
    {
        if (pathOrDll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            _customExecutor = LoadExecutorFromDll(pathOrDll);
        }
        else
        {
            // It's a goal path - use engine to run it
            _customExecutor = async (request) => await Engine.Run(pathOrDll, request);
        }
    }
    
    private Func<HttpRequest, Task<GoalResult>> LoadExecutorFromDll(string dllPath)
    {
        // Load executor from DLL - implementation depends on your plugin architecture
        throw new NotImplementedException("DLL executor loading not implemented");
    }
    
    // Typed convenience methods
    public Task<GoalResult> Get(HttpRequest request)
    {
        request.Method = "GET";
        return Execute("get", request);
    }
    
    public Task<GoalResult> Post(HttpRequest request)
    {
        request.Method = "POST";
        return Execute("post", request);
    }
    
    public Task<GoalResult> Put(HttpRequest request)
    {
        request.Method = "PUT";
        return Execute("put", request);
    }
    
    public Task<GoalResult> Delete(HttpRequest request)
    {
        request.Method = "DELETE";
        return Execute("delete", request);
    }
    
    public Task<GoalResult> Patch(HttpRequest request)
    {
        request.Method = "PATCH";
        return Execute("patch", request);
    }
    
    // Main execute - method string is for routing/logging, HttpRequest has everything
    public override Task<GoalResult> Execute(string method, object? data)
    {
        if (data is not HttpRequest request)
            return GoalResult.ErrorTask("HttpRequest is required");
        
        // Use custom executor if set
        if (_customExecutor != null)
            return _customExecutor(request);
        
        return ExecuteRequest(request);
    }
    
    /// <summary>
    /// Execute the HTTP request. This method can be overridden or replaced via SetExecutor.
    /// </summary>
    protected virtual async Task<GoalResult> ExecuteRequest(HttpRequest request)
    {
        using var client = new HttpClient();
        
        if (request.Timeout.HasValue)
            client.Timeout = TimeSpan.FromMilliseconds(request.Timeout.Value);
        
        var httpRequest = new HttpRequestMessage(
            new HttpMethod(request.Method), 
            request.Url
        );
        
        // Add headers
        if (request.Headers != null)
        {
            foreach (var header in request.Headers)
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        
        // Add body for non-GET requests
        if (request.Data != null && request.Method != "GET")
        {
            var contentType = request.ContentType ?? "application/json";
            
            using var stream = new MemoryStream();
            Serializers[contentType].Serialize(request.Data, stream);
            stream.Position = 0;
            
            httpRequest.Content = new StreamContent(stream);
            httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }
        
        // Execute request
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest);
        }
        catch (TaskCanceledException)
        {
            return GoalResult.Error("Request timed out", statusCode: 408);
        }
        catch (HttpRequestException ex)
        {
            return GoalResult.Error($"Request failed: {ex.Message}", ex);
        }
        
        // Handle error status codes
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return GoalResult.Error(
                $"HTTP {(int)response.StatusCode}: {errorContent}", 
                statusCode: (int)response.StatusCode
            );
        }
        
        // Deserialize response
        var responseType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
        
        try
        {
            using var responseStream = await response.Content.ReadAsStreamAsync();
            
            if (Serializers.Has(responseType))
            {
                var result = Serializers[responseType].Deserialize<object>(responseStream);
                return GoalResult.Success(result);
            }
            else
            {
                // Unknown content type - return as string
                using var reader = new StreamReader(responseStream);
                var text = await reader.ReadToEndAsync();
                return GoalResult.Success(text);
            }
        }
        catch (Exception ex)
        {
            return GoalResult.Error($"Failed to deserialize response: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// HTTP request object - one cohesive object instead of many parameters
/// </summary>
public record HttpRequest(string Url, object? Data = null, Dictionary<string, string>? Headers = null)
{
    public string Method { get; set; } = "GET";
    public int? Timeout { get; set; }
    public string? ContentType { get; set; }
}
