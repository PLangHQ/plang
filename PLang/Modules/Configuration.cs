using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules
{
	public record SettingConfiguration(string Name, string Value, string Comment);
	public record ModuleConfiguration(string Name, string Type, string Comment);
}
