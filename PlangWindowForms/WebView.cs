using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using OpenQA.Selenium.DevTools.V124.WebAudio;
using PLang.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace PLangWindowForms
{
	public class WebView : IUiView
	{
		private readonly WebView2 webView2;

		public WebView(WebView2 webView2, SynchronizationContext? synchronizationContext)
		{
			this.webView2 = webView2;
			this.SynchronizationContext = synchronizationContext;
		}

		public SynchronizationContext? SynchronizationContext { get; set; }

		public async Task Append(string cssSelector, string text, string type = "text", int statusCode = 200, int goalNr = -1)
		{
			string escapedHtmlContent = JsonConvert.ToString(text);
			string script = $@"
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
}
