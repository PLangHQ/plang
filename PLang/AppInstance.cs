using LightInject;
using Newtonsoft.Json;
using PLang.Building;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using Sprache;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Runtime.PseudoRuntime;

namespace PLang
{
	public class AppInstance
	{
		public AppInstance(ServiceContainer container, App app)
		{
			Container = container;
			App = app;
		}
		public ServiceContainer Container { get; set; }
		public App App { get; }

		public IPLangFileSystem FileSystem
		{
			get
			{
				return Container.GetInstance<IPLangFileSystem>();
			}
		}
		public IOutput Ouput
		{
			get
			{
				return Container.GetInstance<IOutput>();
			}
		}

		public IInput Input
		{
			get
			{
				return Container.GetInstance<IInput>();
			}
		}
		public MemoryStack MemoryStack
		{
			get
			{
				return Container.GetInstance<MemoryStack>();
			}
		}
		
	}
