using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using PLang.Interfaces;

namespace PLangWindowForms;

public class WebView : IUiView
{
    private readonly WebView2 webView2;

    public WebView(WebView2 webView2, SynchronizationContext? synchronizationContext)
    {
        this.webView2 = webView2;
        SynchronizationContext = synchronizationContext;
    }

    public SynchronizationContext? SynchronizationContext { get; set; }

    public async Task Append(string cssSelector, string text, string type = "text", int statusCode = 200,
        int goalNr = -1)
    {
        var escapedHtmlContent = JsonConvert.ToString(text);
        var script = $@"
        var targetElement = document.querySelector('{cssSelector}');
        if (targetElement) {{
            targetElement.innerHTML += '{escapedHtmlContent}';
        }}
    ";
        var task = webView2.EnsureCoreWebView2Async();
        await task;
        await webView2.CoreWebView2.ExecuteScriptAsync(script);
    }

    public async Task ExecuteCode(string content)
    {
        var task = webView2.EnsureCoreWebView2Async();
        await task;

        await webView2.CoreWebView2.ExecuteScriptAsync(content);
    }

    public async Task Write(string text, string type = "text", int statusCode = 200, int goalNr = -1)
    {
        throw new NotImplementedException();
    }
}