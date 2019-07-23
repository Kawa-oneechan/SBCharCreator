using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kawa.Json;

namespace SBCharCreator
{
	public static class Humanoid
	{
		public static double[] GlobalOffset { get; private set; }
		public static double[] HeadRunOffset { get; private set; }
		public static double[] HeadSwimOffset { get; private set; }
		public static double RunFallOffset { get; private set; }
		public static double[] BackArmOffset { get; private set; }
		public static object[][] Personalities { get; private set; }

		public static void Load(JsonObj source)
		{
			GlobalOffset = source.Path<double[]>("/globalOffset");
			HeadRunOffset = source.Path<double[]>("/headRunOffset");
			HeadSwimOffset = source.Path<double[]>("/headSwimOffset");
			RunFallOffset = source.Path<double>("/runFallOffset");
			BackArmOffset = source.Path<double[]>("/backArmOffset");
			Personalities = ((List<object>)source["personalities"]).Select(p => ((List<object>)p).ToArray()).ToArray();
		}
	}
}
