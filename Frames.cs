using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kawa.Json;

namespace SBCharCreator
{
	public class Frames
	{
		public int[] Size { get; private set; }
		public Dictionary<string, int[]> Names { get; private set; }

		public Frames(JsonObj source)
		{
			this.Names = new Dictionary<string, int[]>();
			if (source.ContainsKey("frameList"))
			{
				var frameList = (JsonObj)source["frameList"];
				foreach (var frame in frameList)
				{
					var rect = ((List<object>)frame.Value).Select(f => (int)((double)f)).ToArray();
					this.Names.Add((string)frame.Key, new[] { rect[0], rect[1], rect[2] - rect[0], rect[3] - rect[1] });
				}
			}
			else if (source.ContainsKey("frameGrid"))
			{
				var frameGrid = (JsonObj)source["frameGrid"];
				var size = ((List<object>)frameGrid["size"]).Select(f => (int)((double)f)).ToArray();
				this.Size = size;
				var dimensions = ((List<object>)frameGrid["dimensions"]).Select(f => (int)((double)f)).ToArray();
				if (frameGrid.ContainsKey("names"))
				{
					var rowIndex = 0;
					foreach (var row in ((List<object>)frameGrid["names"]).Cast<List<object>>())
					{
						var colIndex = 0;
						foreach (var frame in row)
						{
							if (frame is string)
							{
								Names.Add((string)frame, new[] { colIndex * size[0], rowIndex * size[1], size[0], size[1] });
							}
							colIndex++;
						}
						rowIndex++;
					}
				}
				else
				{
					var i = 0;
					for (var rowIndex = 0; rowIndex < dimensions[1]; rowIndex++)
					{
						for (var colIndex = 0; colIndex < dimensions[0]; colIndex++, i++)
						{
							Names.Add(i.ToString(), new[] { colIndex * size[0], rowIndex * size[1], size[0], size[1] });
						}
					}
				}
			}
			if (source.ContainsKey("aliases"))
			{
				var aliases = (JsonObj)source["aliases"];
				foreach (var alias in aliases)
				{
					Names.Add((string)alias.Key, Names[(string)alias.Value]);
				}
			}
		}
	}
}
