using Stride.Core;
using Stride.Core.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Ocean;
internal class Module
{
	[ModuleInitializer]
	public static void Initialize()
	{
		// Make sure that this assembly is registered
		AssemblyRegistry.Register(typeof(Module).GetTypeInfo().Assembly, AssemblyCommonCategories.Assets);
	}
}
