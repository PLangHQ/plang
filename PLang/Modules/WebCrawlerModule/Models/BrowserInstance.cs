using Microsoft.Playwright;
using UglyToad.PdfPig.Content;

namespace PLang.Modules.WebCrawlerModule.Models
{
	public class BrowserInstance
	{
		private bool disposed;
		int currentPageIndex = 0;

		// Starting a browser costs seconds, so the instance stays open across goals and is not
		// closed when the goal that started it ends. What closes it is an explicit close browser
		// step, or this: every use stamps LastUsed and a timer closes the browser once it has sat
		// unused for IdleTimeout, so a chat that opened one and forgot it does not hold a chromium
		// in memory for the life of the process.
		private readonly Timer idleTimer;
		public TimeSpan IdleTimeout { get; }
		public DateTime LastUsed { get; private set; } = DateTime.UtcNow;

		public BrowserInstance(IPlaywright playwright, IBrowserContext browser, TimeSpan? idleTimeout = null)
		{
			Playwright = playwright;
			this.Browser = browser;
			RouteAsyncByUrl = new();
			IdleTimeout = idleTimeout ?? TimeSpan.FromMinutes(10);
			idleTimer = new Timer(CloseIfIdle, null, IdleTimeout, Timeout.InfiniteTimeSpan);
		}
		public IPlaywright Playwright { get; set; }
		public IBrowserContext Browser { get; set; }
		public bool IsDisposed => disposed;

		public void Touch()
		{
			LastUsed = DateTime.UtcNow;
			if (!disposed) idleTimer.Change(IdleTimeout, Timeout.InfiniteTimeSpan);
		}

		private void CloseIfIdle(object? state)
		{
			if (disposed) return;
			if (DateTime.UtcNow - LastUsed < IdleTimeout)
			{
				idleTimer.Change(IdleTimeout - (DateTime.UtcNow - LastUsed), Timeout.InfiniteTimeSpan);
				return;
			}
			Dispose().GetAwaiter().GetResult();
		}

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

			this.disposed = true;
			idleTimer.Dispose();
			await Browser.DisposeAsync();
			Playwright.Dispose();
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
