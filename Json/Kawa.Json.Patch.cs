using System;
using System.Collections.Generic;
using System.Linq;

namespace Kawa.Json.Patch
{
	public static class JsonPatch
	{
		private static void ParsePath(object o, string operation, string[] path, ref object node, ref string lastKey, ref int lastIndex)
		{
			for (var j = 0; j < path.Length; j++)
			{
				var part = path[j];

				if (part == "" && node == null)
				{
					node = o;
					continue;
				}
				var dummy = 0;
				if (part.Length < 6 && int.TryParse(part, out dummy)) //HACK: "/bodyColor/0/951500" and other such color-related paths should not be taken as indices.
				{
					var index = dummy;
					if (!(node is List<object>))
					{
						throw new JsonException("Invalid patch: tried to navigate into an array, but it's not an array.");
					}
					var n = node as List<object>;
					if (index < n.Count)
					{
						if (j < path.Length - 1)
						{
							node = n[index];
						}
						else
						{
							lastIndex = index;
						}
					}
				}
				else if (part == "-")
				{
					if (!(node is List<object>))
					{
						throw new JsonException("Invalid patch: tried to navigate into an array, but it's not an array.");
					}
					lastIndex = ((List<object>)node).Count;
				}
				else
				{
					if (!(node is JsonObj))
					{
						throw new JsonException("Invalid patch: tried to navigate into an object, but it's not an object.");
					}
					var n = node as JsonObj;
					if (!n.ContainsKey(part))
					{
						if (operation == "add")
						{
							//Gotta make this part first.
							if (j < path.Length - 1)
							{
								if (char.IsDigit(path[j + 1][0]))
								{
									n.Add(part, new List<object>());
								}
								else
								{
									n.Add(part, new JsonObj());
								}
							}
							else
							{
								n.Add(part, new object());
							}
						}
						else
						{
							throw new JsonException("Invalid patch: tried to navigate into a non-existant part, but we're not adding.");
						}
					}
					if (j < path.Length - 1)
					{
						node = n[part];
					}
					else
					{
						lastKey = part;
					}
				}
			}
		}

		public static void Apply(object original, object patch)
		{
			if (!(original is JsonObj))
			{
				throw new ArgumentException("Original must be a JSON Object.");
			}
			if (!(patch is IEnumerable<object>))
			{
				throw new ArgumentException("Patch must be a JSON Array.");
			}
			var o = original as JsonObj;
			var p = (patch as IEnumerable<object>).OfType<JsonObj>().ToArray();
			for (var i = 0; i < p.Length; i++)
			{
				var item = p[i];

				if (!item.ContainsKey("op"))
				{
					throw new JsonException("Invalid patch: item does not specify an operation.");
				}
				var operation = item["op"] as string;
				var path = (item.ContainsKey("path") ? item["path"] as string : string.Empty).Split('/');
				var from = (item.ContainsKey("from") ? item["from"] as string : string.Empty).Split('/');
				var value = item.ContainsKey("value") ? item["value"] : default(object);

				if (operation == "test")
				{
					throw new JsonException("Correct patch, but the test operation is not supported here.");
				}

				if (path.Length == 0)
				{
					throw new JsonException("Invalid patch: add operation does not specify a path.");
				}
				if (value == null && (operation == "add" && operation == "replace" && operation == "remove"))
				{
					throw new JsonException("Invalid patch: add operation does not specify a value.");
				}
				if (operation == "move" && from.Length == 0)
				{
					throw new JsonException("Invalid patch: move operation does not specify a from.");
				}
				var node = default(object);
				var lastKey = string.Empty;
				var lastIndex = -1;
				ParsePath(o, operation, path, ref node, ref lastKey, ref lastIndex);

				if (operation == "add" || operation == "replace")
				{
					if (node is JsonObj)
					{
						((JsonObj)node)[lastKey] = value;
					}
					else if (node is List<object>)
					{
						if (operation == "add")
						{
							((List<object>)node).Insert(lastIndex, value);
						}
						else
						{
							((List<object>)node)[lastIndex] = value;
						}
					}
				}
				else if (operation == "remove")
				{
					if (node is JsonObj)
					{
						((JsonObj)node).Remove(lastKey);
					}
					else if (node is List<object>)
					{
						((List<object>)node).RemoveAt(lastIndex);
					}
				}
				else if (operation == "move")
				{
					var fromNode = default(object);
					var fromLastKey = string.Empty;
					var fromLastIndex = -1;
					ParsePath(o, operation, from, ref fromNode, ref fromLastKey, ref fromLastIndex);
					if (fromNode is JsonObj)
					{
						((JsonObj)node)[lastKey] = ((JsonObj)fromNode)[fromLastKey];
					}
					else if (node is List<Object>)
					{
						((List<object>)node)[lastIndex] = ((List<object>)node)[fromLastIndex];
					}
				}
				else if (operation == "copy")
				{
					var fromNode = default(object);
					var fromLastKey = string.Empty;
					var fromLastIndex = -1;
					ParsePath(o, operation, from, ref fromNode, ref fromLastKey, ref fromLastIndex);
					if (fromNode is JsonObj)
					{
						((JsonObj)node)[lastKey] = ((JsonObj)fromNode)[fromLastKey];
					}
					else if (node is List<Object>)
					{
						((List<object>)node).Insert(lastIndex, ((List<object>)node)[fromLastIndex]);
					}
				}
			}
		}
	}
}
