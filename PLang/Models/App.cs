using LightInject;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Models
{
	public partial class App
	{
		private readonly ServiceContainer container;

		public App(ServiceContainer container)
		{
			this.container = container;

			Engine = container.GetInstance<IEngine>();
			Engine.Init(container);
		}


		public string Id { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public DateTimeOffset Updated { get; set; }
		[JsonIgnore]
		public string AbsolutePath { get;set;}
		[JsonIgnore]
		public string RuntimeId { get; set; }

		[JsonIgnore]
		public List<Goal> Goals { get; set; }

		[JsonIgnore]
		public List<App> Apps { get; set; }
		[JsonIgnore]
		public PLangAppContext Context { get; set; } = new();

		public IEngine Engine { get; }

		public IEnumerable<Goal> GetSetupGoals()
		{
			return Goals.Where(p => p.IsSetup);
		}

		public IEnumerable<Goal> GetGoals()
		{
			return Goals.Where(p => !p.IsSetup);
		}

		public Goal? GetGoal(string goalName)
		{
			return Goals.FirstOrDefault(p => p.GoalName == goalName);
		}

		
	}
}
