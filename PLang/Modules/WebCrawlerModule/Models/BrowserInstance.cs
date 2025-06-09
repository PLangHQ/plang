using Microsoft.Playwright;
using UglyToad.PdfPig.Content;

namespace PLang.Modules.WebCrawlerModule.Models
{
	public class BrowserInstance
	{
		private bool disposed;
		int currentPageIndex = 0;

		public BrowserInstance(IPlaywright playwright, IBrowserContext browser)
		{
			Playwright = playwright;
			this.Browser = browser;
			RouteAsyncByUrl = new();
		}
		public IPlaywright Playwright { get; set; }
		public IBrowserContext Browser { get; set; }

		public async Task<IPage> GetCurrentPage(int idx = -1)
		{
			if (idx == -1) idx = currentPageIndex;

			if (idx > Browser.Pages.Count - 1)
			{
				idx = Browser.Pages.Count - 1;
			}

			if (idx == -1 && Browser.Pages.Count > 0)
			{
				idx = 0;
			}

			if (idx == -1)
			{
				currentPageIndex = 0;
				return await Browser.NewPageAsync();

			}

			currentPageIndex = idx;
			return Browser.Pages[idx];



		}

		public Dictionary<string, RouteAsync> RouteAsyncByUrl { get; set; }

		public async Task Dispose()
		{
			if (this.disposed)
			{
				return;
			}

			await Browser.DisposeAsync();
			Playwright.Dispose();

			this.disposed = true;
		}

		protected virtual void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
		}
	}

	// possible to extend this RouteAsync, e.g. modify body
	public record RouteAsync(List<string> HeadersToRemove);
}
