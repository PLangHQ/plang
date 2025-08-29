using LightInject;
using Microsoft.AspNetCore.Http;
using PLang.Building.Model;
using PLang.Container;
using PLang.Interfaces;
using PLang.Services.OutputStream;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PLang.Runtime
{
	public record EngineInfo(IEngine Engine, DateTime LastAccess);


	public class EnginePool : IDisposable
	{
		private readonly ConcurrentQueue<EngineInfo> _pool = new();

		private readonly Func<IEngine> _factory;
		private readonly int _maxSize;
		private bool disposed;

		public EnginePool(int initialSize, Func<IEngine> factory, int maxSize = 50)
		{
			if (initialSize > maxSize)
				throw new ArgumentException("Initial size cannot exceed max size.");

			_factory = factory ?? throw new ArgumentNullException(nameof(factory));
			_maxSize = maxSize;
			//todo: dont really understand this SemaphoreSlim, seems to work

			_ = Task.Run(async () =>
			{
				while (!disposed)
				{
					await Task.Delay(TimeSpan.FromSeconds(30));
					await CheckPoolSize();
				}
			});

			for (int i = 0; i < initialSize; i++)
			{
				var engine = _factory();
				SetProperties(engine, engine.ParentEngine, null, engine.Name, engine.OutputStreamFactory.CreateHandler());

				_pool.Enqueue(new EngineInfo(engine, DateTime.Now));
			}
		}

		private async Task CheckPoolSize()
		{
			var now = DateTime.UtcNow;
			var keep = new List<EngineInfo>();

			while (_pool.TryDequeue(out var info))
			{
				if (now - info.LastAccess < TimeSpan.FromMinutes(3) || keep.Count < 2)
					keep.Add(info);
				else
				{
					var proc = Process.GetCurrentProcess();
					
					try { 
						info.Engine.Container.Dispose();
					}
					catch (Exception ex)
					{
						Console.WriteLine("Dispose engine on CheckPoolSize:" + ex);

					}
					
				}
			}

			foreach (var item in keep)
				_pool.Enqueue(item);


		}

		public void ReloadGoals()
		{
			foreach (var engineInfo in _pool)
			{
				engineInfo.Engine.PrParser.ForceLoadAllGoals();
				engineInfo.Engine.GetEventRuntime().Reload();
			}
		}

		public async Task<IEngine> RentAsync(IEngine parentEngine, GoalStep? callingStep, string name, IOutputStream? outputStream = null, HttpContext? httpContext = null)
		{
			var proc = Process.GetCurrentProcess();

			if (_pool.TryDequeue(out var engineInfo))
			{
				return SetProperties(engineInfo.Engine, parentEngine, callingStep, name, outputStream, httpContext);
			}
			
			Console.WriteLine($"Create new engine: {_pool.Count}");

			var newEngine = _factory();
			return SetProperties(newEngine, parentEngine, callingStep, name, outputStream, httpContext);
		}

		private IEngine SetProperties(IEngine engine, IEngine parentEngine, GoalStep? callingStep, string name, IOutputStream? outputStream, HttpContext? httpContext = null)
		{
			engine.Name = name;
			engine.SetParentEngine(parentEngine);
			engine.CallbackInfos = parentEngine.CallbackInfos;


			if (outputStream != null)
			{
				engine.SetOutputStream(outputStream);
			}
			else
			{
				outputStream = parentEngine.OutputStreamFactory.CreateHandler();
				engine.SetOutputStream(outputStream);
			}
			engine.OutputStreamFactory.SetEngine(engine);

			if (outputStream is HttpOutputStream hos)
			{
				engine.HttpContext = hos.HttpContext;
			}

			if (callingStep != null)
			{
				engine.SetCallingStep(callingStep);
			}

			
			engine.AddContext("!plang.output", outputStream.Output);			
			engine.AddContext("!plang.osPath", engine.FileSystem.SystemDirectory);
			engine.AddContext("!plang.rootPath", parentEngine?.Path ?? engine.FileSystem.RootDirectory);


			//engine.GetContext().Clear();
			foreach (var item in parentEngine.GetContext())
			{
				engine.GetContext().AddOrReplace(item.Key, item.Value);
			}

			//engine.GetMemoryStack().Clear();
			foreach (var item in parentEngine.GetMemoryStack().GetMemoryStack())
			{
				engine.GetMemoryStack().Put(item, callingStep);
			}

			return engine;
		}

		public void Return(IEngine engine, bool reset = false)
		{
			engine.Return(reset);

			_pool.Enqueue(new EngineInfo(engine, DateTime.Now));
		}

		public virtual void Dispose()
		{
			if (this.disposed)
			{
				return;
			}

			foreach (var item in _pool)
			{
				item.Engine.Dispose();
			}

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
}
