using System;
using System.Linq;
using System.Collections.Generic;

namespace Kawa.Json
{
	public static partial class Json5
	{
		/// <summary>
		/// Returns an object of type T found in the specified path.
		/// </summary>
		/// <typeparam name="T">The type of object to return.</typeparam>
		/// <param name="obj">The JsonObj to look through.</param>
		/// <param name="target">The path to follow.</param>
		/// <returns>The object at the end of the path.</returns>
		/// <remarks>
		/// If obj is a Starbound Versioned JSON object, if the first key is not found,
		/// Path will automatically try skipping into the __content object.
		/// </remarks>
		public static T Path<T>(this JsonObj obj, string target)
		{
			if (string.IsNullOrWhiteSpace(target))
			{
				throw new ArgumentException("Path is empty.");
			}
			if (!target.StartsWith("/"))
			{
				throw new ArgumentException("Path does not start with root.");
			}
			if (target.EndsWith("/"))
			{
				throw new ArgumentException("Path does not end with a key or index.");
			}
			var parts = target.Substring(1).Split('/');
			object root = obj;
			var here = root;
			int index;
			foreach (var part in parts)
			{
				if (part == "-")
				{
					throw new JsonException("Can't use - here; we're not patching anything.");
				}
				bool isIndex = int.TryParse(part, out index);
				if (isIndex && here == root)
				{
					throw new JsonException("Tried to start with an array index. That's extra special.");
				}
				if (isIndex && here is object[])
				{
					var list = (object[])here;
					if (index < 0 || index >= list.Length)
					{
						throw new JsonException("Index out of range.");
					}
					here = list[index];
				}
				else if (isIndex && here is List<object>)
				{
					var list = (List<object>)here;
					if (index < 0 || index >= list.Count)
					{
						throw new IndexOutOfRangeException();
					}
					here = list[index];
				}
				else if (here is JsonObj)
				{
					var map = (JsonObj)here;
					if (here == root && map.ContainsKey("__content") && !map.ContainsKey(part))
					{
						//Sneakily stealthily skip into this.
						map = (JsonObj)map["__content"];
					}
					if (!map.ContainsKey(part))
					{
						throw new KeyNotFoundException();
					}
					here = map[part];
				}
				else
				{
					throw new JsonException("Current node is not an array or object, but path isn't done yet.");
				}
			}

			if (typeof(T).Name == "Int32" && here is double)
			{
				here = (int)(double)here;
			}
			if (typeof(T).Name == "Double" && here is double)
			{
				here = (double)here;
			}
			if (typeof(T).Name == "Boolean" && here is bool)
			{
				here = (bool)here;
			}
			else if (typeof(T).Name == "Int32[]" && here is List<object>)
			{
				here = ((List<object>)here).Select(x => (int)(double)x).ToArray();
			}
			else if (typeof(T).Name == "Double[]" && here is List<object>)
			{
				here = ((List<object>)here).Select(x => (double)x).ToArray();
			}
			else if (typeof(T).Name == "Boolean[]" && here is List<object>)
			{
				here = ((List<object>)here).Select(x => (bool)x).ToArray();
			}
			else if (typeof(T).Name == "String[]" && here is List<object>)
			{
				here = ((List<object>)here).Select(x => (string)x).ToArray();
			}
			else if (typeof(T).Name == "JsonObj[]" && here is List<object>)
			{
				here = ((List<object>)here).Cast<JsonObj>().ToArray();
			}
			else if (typeof(T).Name == "Object[]" && here is List<object>)
			{
				here = ((List<object>)here).ToArray();
			}
#if XNA
			else if (typeof(T).Name == "Vector2" && here is List<object>)
			{
				here = new Microsoft.Xna.Framework.Vector2((float)(double)((List<object>)here)[0], (float)(double)((List<object>)here)[1]);
			}
			else if (typeof(T).Name == "Rectangle" && here is List<object>)
			{
				here = new Microsoft.Xna.Framework.Rectangle((int)(double)((List<object>)here)[0], (int)(double)((List<object>)here)[1], (int)(double)((List<object>)here)[2], (int)(double)((List<object>)here)[3]);
			}
#endif
			else if (typeof(T).Name == "List`1")
			{
				var contained = typeof(T).GetGenericArguments()[0];
				var hereList = (List<object>)here;
				switch (contained.Name)
				{
					case "Int32":
						here = hereList.Select(x => (int)(double)x).ToList();
						break;
					case "Double":
						here = hereList.Select(x => (double)x).ToList();
						break;
					case "Boolean":
						here = hereList.Select(x => (bool)x).ToList();
						break;
					case "String":
						here = hereList.Select(x => (string)x).ToList();
						break;
					case "JsonObj":
						here = hereList.Select(x => (JsonObj)x).ToList();
						break;
					default:
						here = hereList;
						break;
				}
			}

			if (!(here is T))
			{
				throw new JsonException(string.Format("Value at end of path is not of the requested type -- found {0} but expected {1}.", here.GetType(), typeof(T)));
			}
			return (T)here;
		}

		public static T Path<T>(this JsonObj obj, string target, T replacement)
		{
			try
			{
				return Path<T>(obj, target);
			}
			catch (KeyNotFoundException)
			{
				return replacement;
			}
		}

		/// <summary>
		/// Returns the JsonObj found in the specified path.
		/// </summary>
		/// <param name="obj">The JsonObj to look through.</param>
		/// <param name="target">The path to follow.</param>
		/// <returns>The JsonObj at the end of the path.</returns>
		/// <remarks>
		/// If obj is a Starbound Versioned JSON object, if the first key is not found,
		/// Path will automatically try skipping into the __content object.
		/// </remarks>
		public static JsonObj Path(this JsonObj obj, string target)
		{
			return Path<JsonObj>(obj, target);
		}
	}
}
