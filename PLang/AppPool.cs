using LightInject;
using PLang.Container;
using PLang.Interfaces;
using PLang.Models;
using PLang.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PLang
{
	public interface IAppPoolSettings {
		public string AbsoluteAppPath { get; }
		public string Output { get; }
		public int PoolSize { get; set; }
		public int MaxPoolSize { get; set; }
	}
	public class WindowAppPoolSettings : AppPoolSettings, IAppPoolSettings
	{
		public WindowAppPoolSettings(string absoluteAppPath, IAskUserDialog askUserDialog, IErrorDialog errorDialog, IForm iForm, List<string>? args = null) : base(absoluteAppPath, "window", args)
		{
			AskUserDialog = askUserDialog;
			ErrorDialog = errorDialog;
			IForm = iForm;
		}

		public IAskUserDialog AskUserDialog { get; }
		public IErrorDialog ErrorDialog { get; }
		public IForm IForm { get; }
	}
	public class WebAppPoolSettings : AppPoolSettings, IAppPoolSettings
	{
		public WebAppPoolSettings(string absoluteAppPath, HttpListenerContext httpContext, List<string>? args = null) : base(absoluteAppPath, "web", args)
		{
			HttpContext = httpContext;
		}

		public HttpListenerContext HttpContext { get; }
	}
	public class AppPoolSettings : IAppPoolSettings
	{
		
		public AppPoolSettings(string absoluteAppPath, string output, List<string>? args = null)
		{
			AbsoluteAppPath = absoluteAppPath;
			Output = output;
		}

		public string AbsoluteAppPath { get; }
		public string Output { get; }
		public int PoolSize { get; set; } = 1;
		public int MaxPoolSize { get; set; } = 10;
		public AppInstance ParentApp { get; set; }
	}

	public sealed class AppPool : IDisposable
	{
		private bool disposed;
		private ServiceContainer container;

		public IAppPoolSettings Settings { get; }

		public AppPool(IAppPoolSettings settings)
		{
			container = new PlangContainer(settings.AbsoluteAppPath);
			if (settings is WebAppPoolSettings webSettings)
			{
				container.RegisterForPLangWebserver(webSettings.AbsoluteAppPath, "/", webSettings.HttpContext, webSettings.HttpContext.Response.ContentType ?? "text/html");
			}
			else if (settings is WindowAppPoolSettings winSettings)
			{
				container.RegisterForPLangWindowApp(winSettings.AbsoluteAppPath, "/", winSettings.AskUserDialog, winSettings.ErrorDialog, winSettings.IForm);
			}
			else
			{
				container.RegisterForPLangConsole(settings.AbsoluteAppPath, "/");
			}

			Settings = settings;
		}

		ConcurrentBag<App> appPools = new();
		public async Task<App> Rent(List<string> args)
		{
			if (!appPools.TryTake(out var app))
			{
				app = new App(container);
			}
			app.RegisterArgs(args);			
			return app;
		}

		public void Return(App app)
		{
			if (appPools.Count >= Settings.MaxPoolSize) return;

			appPools.Add(app);
		}

		public void Dispose()
		{
			if (this.disposed)
			{
				return;
			}
			this.container.Dispose();

			this.disposed = true;
		}

		private void ThrowIfDisposed()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}
		}
	}
}
